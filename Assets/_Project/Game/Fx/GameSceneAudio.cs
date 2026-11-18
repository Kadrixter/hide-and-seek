using HideAndSeek.Audio;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HideAndSeek.Game
{
    /// <summary>Placed in each scene: applies the acoustic environment (reverb, echo, bed) and, for maps, ambient emitters.</summary>
    public class GameSceneAudio : MonoBehaviour
    {
        [SerializeField] private AudioEnvironmentKind kind = AudioEnvironmentKind.Garage;
        [SerializeField] private bool populateEmitters = true;

        private void Awake()
        {
            // Maps with working elevators (markers ElevatorCar_*).
            if (populateEmitters && GetComponent<ElevatorSystem>() == null) gameObject.AddComponent<ElevatorSystem>();
        }

        private void Start()
        {
            AudioEnvironment.Apply(kind, true);
            if (!populateEmitters) return;
            foreach (var root in gameObject.scene.GetRootGameObjects())
                if (root != gameObject && root.GetComponentInChildren<MeshRenderer>() != null)
                    AmbientEmitters.Populate(root.transform);
        }
    }
}
