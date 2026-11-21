using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Steamworks;
using Steamworks.Data;
using Unity.Netcode;
using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;

// Facepunch Transport for Netcode for GameObjects (Unity multiplayer-community-contributions, MIT).
// Hide and Seek changes: the Steam client lifetime is owned by the game (SteamService) - the transport no longer
// initialises / shuts down Steam or pumps callbacks, so Steam lobbies survive leaving a match; Shutdown() only closes
// sockets; StartClient fails cleanly without a target; GetCurrentRtt reports the real ping.
namespace Netcode.Transports.Facepunch
{
    using SocketConnection = Connection;

    public class FacepunchTransport : NetworkTransport, IConnectionManager, ISocketManager
    {
        private ConnectionManager connectionManager;
        private SocketManager socketManager;
        private Dictionary<ulong, Client> connectedClients;
        private bool m_SteamInitialized;

        [Space]
        [Tooltip("The Steam App ID of your game. Technically you're not allowed to use 480, but Valve doesn't do anything about it so it's fine for testing purposes.")]
        [SerializeField] private uint steamAppId = 480;

        [Tooltip("The Steam ID of the user targeted when joining as a client.")]
        [SerializeField] public ulong targetSteamId;

        [Header("Info")]
        [ReadOnly]
        [Tooltip("When in play mode, this will display your Steam ID.")]
        [SerializeField] private ulong userSteamId;

        private LogLevel LogLevel => NetworkManager.Singleton != null ? NetworkManager.Singleton.LogLevel : LogLevel.Normal;

        private class Client
        {
            public SteamId steamId;
            public SocketConnection connection;
        }

        #region NetworkTransport Overrides

        public override ulong ServerClientId => 0;

        public override void DisconnectLocalClient()
        {
            connectionManager?.Connection.Close();
            connectionManager = null;

            if (LogLevel <= LogLevel.Developer)
                Debug.Log($"[{nameof(FacepunchTransport)}] - Disconnecting local client.");
        }

        public override void DisconnectRemoteClient(ulong clientId)
        {
            if (connectedClients.TryGetValue(clientId, out Client user))
            {
                // Flush any pending messages before closing the connection
                user.connection.Flush();
                user.connection.Close();
                connectedClients.Remove(clientId);

                if (LogLevel <= LogLevel.Developer)
                    Debug.Log($"[{nameof(FacepunchTransport)}] - Disconnecting remote client with ID {clientId}.");
            }
            else if (LogLevel <= LogLevel.Normal)
                Debug.LogWarning($"[{nameof(FacepunchTransport)}] - Failed to disconnect remote client with ID {clientId}, client not connected.");
        }

        public override ulong GetCurrentRtt(ulong clientId)
        {
            try
            {
                if (clientId == ServerClientId && connectionManager != null)
                    return (ulong)Mathf.Max(0, connectionManager.Connection.QuickStatus().Ping);
                if (connectedClients != null && connectedClients.TryGetValue(clientId, out Client c))
                    return (ulong)Mathf.Max(0, c.connection.QuickStatus().Ping);
            }
            catch { /* not connected yet */ }
            return 0;
        }

        public override void Initialize(NetworkManager networkManager = null)
        {
            connectedClients = new Dictionary<ulong, Client>();
            if (SteamClient.IsValid) userSteamId = SteamClient.SteamId;
            else Debug.LogError($"[{nameof(FacepunchTransport)}] - Steam is not running / not initialised (app {steamAppId}).");
        }

        private SendType NetworkDeliveryToSendType(NetworkDelivery delivery)
        {
            return delivery switch
            {
                NetworkDelivery.Reliable => SendType.Reliable,
                NetworkDelivery.ReliableFragmentedSequenced => SendType.Reliable,
                NetworkDelivery.ReliableSequenced => SendType.Reliable,
                NetworkDelivery.Unreliable => SendType.Unreliable,
                NetworkDelivery.UnreliableSequenced => SendType.Unreliable,
                _ => SendType.Reliable
            };
        }

        public override void Shutdown()
        {
            try
            {
                if (LogLevel <= LogLevel.Developer)
                    Debug.Log($"[{nameof(FacepunchTransport)}] - Shutting down.");

                connectionManager?.Close();
                socketManager?.Close();
                connectionManager = null;
                socketManager = null;
                connectedClients?.Clear();
            }
            catch (Exception e)
            {
                if (LogLevel <= LogLevel.Error)
                    Debug.LogError($"[{nameof(FacepunchTransport)}] - Caught an exception while shutting down: {e}");
            }
        }

        public override void Send(ulong clientId, ArraySegment<byte> data, NetworkDelivery delivery)
        {
	        var sendType = NetworkDeliveryToSendType(delivery);

	        if (clientId == ServerClientId)
		        connectionManager.Connection.SendMessage(data.Array, data.Offset, data.Count, sendType);
	        else if (connectedClients.TryGetValue(clientId, out Client user))
		        user.connection.SendMessage(data.Array, data.Offset, data.Count, sendType);
	        else if (LogLevel <= LogLevel.Normal)
		        Debug.LogWarning($"[{nameof(FacepunchTransport)}] - Failed to send packet to remote client with ID {clientId}, client not connected.");
        }

        public override NetworkEvent PollEvent(out ulong clientId, out ArraySegment<byte> payload, out float receiveTime)
        {
            connectionManager?.Receive();
            socketManager?.Receive();

            clientId = 0;
            receiveTime = Time.realtimeSinceStartup;
            payload = default;
            return NetworkEvent.Nothing;
        }

        public override bool StartClient()
        {
            if (LogLevel <= LogLevel.Developer)
                Debug.Log($"[{nameof(FacepunchTransport)}] - Starting as client.");

            if (!SteamClient.IsValid || targetSteamId == 0) return false;
            connectionManager = SteamNetworkingSockets.ConnectRelay<ConnectionManager>(targetSteamId);
            if (connectionManager == null) return false;
            connectionManager.Interface = this;
            return true;
        }

        public override bool StartServer()
        {
            if (LogLevel <= LogLevel.Developer)
                Debug.Log($"[{nameof(FacepunchTransport)}] - Starting as server.");

            if (!SteamClient.IsValid) return false;
            socketManager = SteamNetworkingSockets.CreateRelaySocket<SocketManager>();
            if (socketManager == null) return false;
            socketManager.Interface = this;
            return true;
        }

        #endregion

        #region ConnectionManager Implementation

        private byte[] payloadCache = new byte[4096];

        private void EnsurePayloadCapacity(int size)
        {
            if (payloadCache.Length >= size)
                return;

            payloadCache = new byte[Math.Max(payloadCache.Length * 2, size)];
        }

        void IConnectionManager.OnConnecting(ConnectionInfo info)
        {
            if (LogLevel <= LogLevel.Developer)
                Debug.Log($"[{nameof(FacepunchTransport)}] - Connecting with Steam user {info.Identity.SteamId}.");
        }

        void IConnectionManager.OnConnected(ConnectionInfo info)
        {
            InvokeOnTransportEvent(NetworkEvent.Connect, ServerClientId, default, Time.realtimeSinceStartup);

            if (LogLevel <= LogLevel.Developer)
                Debug.Log($"[{nameof(FacepunchTransport)}] - Connected with Steam user {info.Identity.SteamId}.");
        }

        void IConnectionManager.OnDisconnected(ConnectionInfo info)
        {
            InvokeOnTransportEvent(NetworkEvent.Disconnect, ServerClientId, default, Time.realtimeSinceStartup);

            if (LogLevel <= LogLevel.Developer)
                Debug.Log($"[{nameof(FacepunchTransport)}] - Disconnected Steam user {info.Identity.SteamId}.");
        }

        unsafe void IConnectionManager.OnMessage(IntPtr data, int size, long messageNum, long recvTime, int channel)
        {
            EnsurePayloadCapacity(size);

            fixed (byte* payload = payloadCache)
            {
                UnsafeUtility.MemCpy(payload, (byte*)data, size);
            }

            InvokeOnTransportEvent(NetworkEvent.Data, ServerClientId, new ArraySegment<byte>(payloadCache, 0, size), Time.realtimeSinceStartup);
        }

        #endregion

        #region SocketManager Implementation

        void ISocketManager.OnConnecting(SocketConnection connection, ConnectionInfo info)
        {
            if (LogLevel <= LogLevel.Developer)
                Debug.Log($"[{nameof(FacepunchTransport)}] - Accepting connection from Steam user {info.Identity.SteamId}.");

            connection.Accept();
        }

        void ISocketManager.OnConnected(SocketConnection connection, ConnectionInfo info)
        {
            if (!connectedClients.ContainsKey(connection.Id))
            {
                connectedClients.Add(connection.Id, new Client()
                {
                    connection = connection,
                    steamId = info.Identity.SteamId
                });

                InvokeOnTransportEvent(NetworkEvent.Connect, connection.Id, default, Time.realtimeSinceStartup);

                if (LogLevel <= LogLevel.Developer)
                    Debug.Log($"[{nameof(FacepunchTransport)}] - Connected with Steam user {info.Identity.SteamId}.");
            }
            else if (LogLevel <= LogLevel.Normal)
                Debug.LogWarning($"[{nameof(FacepunchTransport)}] - Failed to connect client with ID {connection.Id}, client already connected.");
        }

        void ISocketManager.OnDisconnected(SocketConnection connection, ConnectionInfo info)
        {
            if (connectedClients.Remove(connection.Id))
	    {
	        InvokeOnTransportEvent(NetworkEvent.Disconnect, connection.Id, default, Time.realtimeSinceStartup);

	       if (LogLevel <= LogLevel.Developer)
                    Debug.Log($"[{nameof(FacepunchTransport)}] - Disconnected Steam user {info.Identity.SteamId}");
	    }
     	    else if (LogLevel <= LogLevel.Normal)
                Debug.LogWarning($"[{nameof(FacepunchTransport)}] - Failed to diconnect client with ID {connection.Id}, client not connected.");
        }

        unsafe void ISocketManager.OnMessage(SocketConnection connection, NetIdentity identity, IntPtr data, int size, long messageNum, long recvTime, int channel)
        {
            EnsurePayloadCapacity(size);

            fixed (byte* payload = payloadCache)
            {
                UnsafeUtility.MemCpy(payload, (byte*)data, size);
            }

            InvokeOnTransportEvent(NetworkEvent.Data, connection.Id, new ArraySegment<byte>(payloadCache, 0, size), Time.realtimeSinceStartup);
        }

        #endregion

    }
}
