namespace GudKoodi.DeeperSkeeper.Network
{
    using GudKoodi.DeeperSkeeper.Enemy;
    using UnityEngine;

    /// <summary>
    /// Class for managing objects serialized as <see cref="Enemy" />.
    /// </summary>
    public class EnemyManager : ObjectManager<Enemy>
    {
        private readonly PlayerManager playerManager;

        public EnemyManager(PlayerManager playerManager) : base()
        {
            this.playerManager = playerManager;
        }

        /// <summary>
        /// Deserializes the state to the GameObject from <see cref="Enemy" /> object.
        /// </summary>
        /// <param name="enemy">Data to deserialize from.</param>
        /// <param name="gameObject">GameObject to update.</param>
        protected override void DeserializeState(Enemy enemy, GameObject gameObject)
        {
            gameObject.transform.position = enemy.CurrentPosition;
            if (enemy.Target > 0)
            {
                gameObject.GetComponent<EnemyController>().StartChase(this.playerManager[enemy.Target]);
            }
        }

        /// <summary>
        /// Instantiates a new enemy GameObject and gives it proper components for local controlling.
        /// </summary>
        /// <param name="prefab">Prefab used as a base for the new GameObject.</param>
        /// <param name="enemy">Serialization data used in creation.</param>
        /// <returns>Instantiated GameObject.</returns>
        protected override GameObject InstantiateMaster(GameObject prefab, Enemy enemy)
        {
            GameObject go = GameObject.Instantiate(prefab, enemy.CurrentPosition, Quaternion.identity);
            return go;
        }

        /// <summary>
        /// Instantiates a new enemy GameObject and gives it proper components for remote controlling.
        /// </summary>
        /// <param name="prefab">Prefab used as a base for the new GameObject.</param>
        /// <param name="enemy">Serialization data used in creation.</param>
        /// <returns>Instantiated GameObject.</returns>
        protected override GameObject InstantiateSlave(GameObject prefab, Enemy enemy)
        {
            GameObject go = GameObject.Instantiate(prefab, enemy.CurrentPosition, Quaternion.identity);
            DeserializeState(enemy, go);
            return go;
        }

        /// <summary>
        /// Serializes the GameObject to a <see cref="Enemy" /> object.
        /// </summary>
        /// <param name="enemy">Data to serialize into.</param>
        /// <param name="gameObject">GameObject to serialize from.</param>
        protected override void SerializeState(Enemy enemy, GameObject gameObject)
        {
            enemy.CurrentPosition = gameObject.transform.position;
            GameObject target = gameObject.GetComponent<EnemyController>().player;
            enemy.Target = (target == null) ? (ushort)0 : playerManager.GetNetworkID(target);
        }
    }
}
