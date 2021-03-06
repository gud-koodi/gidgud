namespace GudKoodi.DeeperSkeeper.Network
{
    using UnityEngine;
    using Event;
    using DeeperSkeeper.Player;
    using Weapon;

    /// <summary>
    /// Class for managing objects serialized as <see cref="Player" />.
    /// </summary>
    public class PlayerManager : ObjectManager<Player>
    {
        /// <summary>
        /// Event called after a master object has been created.
        /// </summary>
        private readonly ObjectCreated masterPlayerCreated;

        /// <summary>
        /// Event called when remote controlled player should attack.
        /// </summary>
        private readonly ObjectUpdateRequested attackStarted;

        /// <summary>
        /// Event called when a master object requests an update to network.
        /// </summary>
        private readonly ObjectUpdateRequested objectUpdateRequested;

        /// <summary>
        /// Initializes a new instance of the <see cref="PlayerManager"/> class.
        /// </summary>
        /// <param name="attackStarted">Event that should be called when player performs an attack.</param>
        /// <param name="masterPlayerCreated">Event that should be called after a master object has been created.</param>
        /// <param name="objectUpdateRequested">Event that should be called when a master object requests an update to network.</param>
        public PlayerManager(ObjectUpdateRequested attackStarted, ObjectCreated masterPlayerCreated, ObjectUpdateRequested objectUpdateRequested) : base()
        {
            this.attackStarted = attackStarted;
            this.masterPlayerCreated = masterPlayerCreated;
            this.objectUpdateRequested = objectUpdateRequested;
        }

        /// <summary>
        /// Starts attack animation for givent object. Quick temporary solution.
        /// </summary>
        /// <param name="networkID">ID of the object that should play attack animation.</param>
        public void SpaghettiAttack(ushort networkID)
        {
            GameObject go = base[networkID];
            go.GetComponent<NetworkSlave>().StartAttack();
        }

        /// <summary>
        /// Instantiates a new GameObject and gives it proper components for local controlling.
        /// </summary>
        /// <param name="prefab">Prefab used as a base for the new GameObject.</param>
        /// <param name="player">Serialization data used in creation.</param>
        /// <returns>Instantiated GameObject.</returns>
        protected override GameObject InstantiateMaster(GameObject prefab, Player player)
        {
            GameObject go = GameObject.Instantiate(prefab, player.CurrentPosition, Quaternion.identity);

            go.AddComponent<PlayerController>();
            PlayerController pc = go.GetComponent<PlayerController>();
            pc.playerRigidbody = go.GetComponent<Rigidbody>();
            pc.weapon = go.GetComponentInChildren<Weapon>();
            pc.AttackStarted = this.attackStarted;

            NetworkMaster master = go.AddComponent<NetworkMaster>();
            master.ObjectUpdateRequested = this.objectUpdateRequested;

            // TODO: Do this call elsewhere, but for now having a direct access to its reference is sooo niice.
            this.masterPlayerCreated.Trigger(go);
            return go;
        }

        /// <summary>
        /// Instantiates a new GameObject and gives it proper components for remote controlling.
        /// </summary>
        /// <param name="prefab">Prefab used as a base for the new GameObject.</param>
        /// <param name="player">Serialization data used in creation.</param>
        /// <returns>Instantiated GameObject.</returns>
        protected override GameObject InstantiateSlave(GameObject prefab, Player player)
        {
            GameObject go = GameObject.Instantiate(prefab, player.CurrentPosition, Quaternion.identity);

            NetworkSlave slave = go.AddComponent<NetworkSlave>();
            slave.Rigidbody = go.GetComponent<Rigidbody>();
            slave.UpdateState(player);
            return go;
        }

        /// <summary>
        /// Deserializes the state to the GameObject from <see cref="Player" /> object.
        /// </summary>
        /// <param name="data">Data to deserialize from.</param>
        /// <param name="gameObject">GameObject to update.</param>
        protected override void DeserializeState(Player player, GameObject gameObject)
        {
            NetworkSlave slave = gameObject.GetComponent<NetworkSlave>();
            slave.UpdateState(player);
        }

        /// <summary>
        /// Serializes the GameObject to a <see cref="Player" /> object.
        /// </summary>
        /// <param name="data">Data to serialize into.</param>
        /// <param name="gameObject">GameObject to serialize from.</param>
        protected override void SerializeState(Player player, GameObject gameObject)
        {
            player.CurrentPosition = gameObject.transform.localPosition;
            player.Rotation = gameObject.GetComponent<Rigidbody>().rotation.eulerAngles.y;
            player.TargetPosition = gameObject.transform.localPosition;
        }
    }
}
