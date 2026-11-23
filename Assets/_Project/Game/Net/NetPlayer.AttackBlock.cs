namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// The ONE place that says the seeker cannot attack (knife / chainsaw) right now - checked by the server in
    /// MatchManager.ServerKnife / ServerChainsaw. Add new reasons here (each with its own state): dust in the eyes
    /// (BlindUntil), toy-gun goo, knocked out by a thrown leg (StunAt, also Busy) - and e.g. the fire extinguisher.
    /// </summary>
    public partial class NetPlayer
    {
        public bool CannotAttack
        {
            get
            {
                var mm = MatchManager.Instance;
                double now = mm != null ? mm.Now : 0d;
                return now < BlindUntil.Value || Gooed || Stunned || FoamBlocksAttack; // + fire-extinguisher foam (NetPlayer.Extinguisher)
            }
        }
    }
}
