namespace GudKoodi.DeeperSkeeper.Network
{
    using System;
    using System.Collections.Generic;
    using DarkRift;
    using DarkRift.Server;
    using DarkRift.Server.Unity;
    using Event;
    using UnityEngine;

    /// <summary>
    /// Component that handles all communication between the server and all clients.
    /// </summary>
    public class ServerController : MonoBehaviour
    {
        /// <summary>
        /// The server component that this script will control.
        /// </summary>
        [Tooltip("The server component that this script will control.")]
        public XmlUnityServer Server;

        /// <summary>
        /// Provider of network object prefabs.
        /// </summary>
        [Tooltip("Provider of network object prefabs.")]
        public NetworkInstantiator NetworkInstantiator;

        /// <summary>
        /// Level generation request event.
        /// </summary>
        [Tooltip("Level generation request event")]
        public LevelGenerationRequested LevelGenerationRequested;

        /// <summary>
        /// Network configuration to check hosting status from.
        /// </summary>
        [Tooltip("Network configuration to check hosting status from.")]
        public NetworkConfig NetworkConfig;

        /// <summary>
        /// Network Events container.
        /// </summary>
        [Tooltip("Network Events container.")]
        public NetworkEvents NetworkEvents;

        /// <summary>
        /// Client IDs mapped to their respective player ids.
        /// </summary>
        /// <typeparam name="ushort">Client ID.</typeparam>
        /// <typeparam name="ushort">Player network ID.</typeparam>
        /// <returns>Map of clients IDs to player network IDs.</returns>
        private readonly Dictionary<ushort, ushort> clientToPlayerObject = new Dictionary<ushort, ushort>();

        /// <summary>
        /// ID provider for enemy manager.
        /// </summary>
        private readonly NetworkIdPool playerIDPool = new NetworkIdPool();

        /// <summary>
        /// ID provider for player manager.
        /// </summary>
        private readonly NetworkIdPool enemyIDPool = new NetworkIdPool();

        /// <summary>
        /// Enemy manager.
        /// </summary>
        private EnemyManager enemies;

        /// <summary>
        /// Player manager.
        /// </summary>
        private PlayerManager players;

        /// <summary>
        /// Level seed for the game.
        /// </summary>
        private int levelSeed = -1;

        /// <summary>
        /// Initializes the server and starts accepting connections.
        /// </summary>
        public void Initialize()
        {
            if (Server == null)
            {
                Debug.LogError("Server component missing.");
                return;
            }

            this.levelSeed = new System.Random().Next();
            this.LevelGenerationRequested.Trigger(levelSeed);

            ushort id = playerIDPool.Next();
            Player player = new Player(id, Vector3.zero, 0);
            players.Create(this.NetworkInstantiator.PlayerPrefab, player, true);

            DarkRiftServer server = Server.Server;
            server.ClientManager.ClientConnected += OnClientConnect;
            server.ClientManager.ClientDisconnected += OnClientDisconnect;
        }

        /// <summary>
        /// Updates the serialization data of given object and sends it to all clients.
        /// </summary>
        /// <param name="gameObject">Object to update the data for.</param>
        /// <param name="objectType">Type of object.</param>
        public void SendObject(GameObject gameObject, ObjectType objectType)
        {
            //// Debug.Log($"Sending {gameObject} of {objectType}");

            Message message = null;
            switch (objectType)
            {
                case ObjectType.Enemy:
                    message = enemies.UpdateAndSerialize(gameObject, ServerMessage.UpdateEnemy);
                    break;
                case ObjectType.Player:
                    message = players.UpdateAndSerialize(gameObject, ServerMessage.UpdatePlayer);
                    break;
                default:
                    Debug.LogError("TODO: Writer error message");
                    break;
            }

            foreach (var client in Server.Server.ClientManager.GetAllClients())
            {
                client.SendMessage(message, SendMode.Unreliable);
            }

            message.Dispose();
        }

        void Awake()
        {
            if (!this.NetworkConfig.isHost)
            {
                Debug.Log("Server manager shutting up.");
                gameObject.SetActive(false);
                return;
            }
            this.players = new PlayerManager(this.NetworkEvents.AttackStarted, this.NetworkInstantiator.MasterPlayerCreated, this.NetworkInstantiator.PlayerUpdateRequested);
            this.enemies = new EnemyManager(players);
            this.NetworkEvents.EnemyCreationRequested.Subscribe(this.EnemyCreationRequested);
            this.NetworkEvents.LevelStartRequested.Subscribe(this.LevelStartRequested);
            this.NetworkEvents.ObjectDestructionRequested.Subscribe(DestroyObject);
            this.NetworkEvents.AttackStarted.Subscribe(AttackStarted);
        }

        void OnDestroy()
        {
            DarkRiftServer server = Server.Server;
            if (server != null)
            {
                server.ClientManager.ClientConnected -= OnClientConnect;
                server.ClientManager.ClientDisconnected -= OnClientDisconnect;
            }
        }

        private void OnClientConnect(object sender, ClientConnectedEventArgs e)
        {
            e.Client.MessageReceived += OnMessageReceived;
            ushort id = playerIDPool.Next();
            Player player = new Player(id, Vector3.zero, 0);
            players.Create(this.NetworkInstantiator.PlayerPrefab, player);

            ConnectionData data = new ConnectionData(e.Client.ID, enemies.ToArray(), id, levelSeed, players.ToArray());
            clientToPlayerObject[data.ClientID] = data.PlayerObjectID;
            using (Message message = Message.Create(ServerMessage.ConnectionData, data))
            {
                e.Client.SendMessage(message, SendMode.Reliable);
            }

            using (Message broadcast = Message.Create(ServerMessage.CreatePlayer, player))
            {
                foreach (var client in Server.Server.ClientManager.GetAllClients())
                {
                    if (client.ID != e.Client.ID)
                    {
                        client.SendMessage(broadcast, SendMode.Reliable);
                    }
                }
            }
        }

        private void OnClientDisconnect(object sender, ClientDisconnectedEventArgs e)
        {
            e.Client.MessageReceived -= OnMessageReceived;
            ushort playerId = clientToPlayerObject[e.Client.ID];
            players.Destroy(playerId);
            playerIDPool.Release(playerId);
            Debug.Log($"Sending destruct message for player ID {playerId}");
            using (DarkRiftWriter writer = DarkRiftWriter.Create())
            {
                writer.Write(playerId);
                using (Message message = Message.Create(ServerMessage.DeletePlayer, writer))
                {
                    foreach (var client in Server.Server.ClientManager.GetAllClients())
                    {
                        if (client.ID != e.Client.ID)
                        {
                            client.SendMessage(message, SendMode.Reliable);
                        }
                    }
                }
            }
        }

        private void SendNetworkID(ushort networkID, ushort messageTag, SendMode sendMode, ushort exclude = 0xFFFF)
        {
            using (var writer = DarkRiftWriter.Create())
            {
                writer.Write(networkID);
                using (var message = Message.Create(messageTag, writer))
                {
                    foreach (var client in Server.Server.ClientManager.GetAllClients())
                    {
                        if (client.ID != exclude)
                        {
                            client.SendMessage(message, sendMode);
                        }
                    }
                }
            }
        }

        private void OnMessageReceived(object sender, MessageReceivedEventArgs e)
        {
            switch (e.Tag)
            {
                case ClientMessage.LevelStartRequest:
                    this.LevelStartRequested(null, null, null, null);
                    break;
                case ClientMessage.UpdatePlayer:
                    UpdatePlayer(e);
                    break;
                case ClientMessage.PlayAttackPlayer:
                    PlayAttackPlayer(e);
                    break;
                case ServerMessage.DeletePlayer:
                    DeleteObject(e, ObjectType.Player);
                    break;
                case ServerMessage.DeleteEnemy:
                    DeleteObject(e, ObjectType.Enemy);
                    break;
            }
        }

        private ushort UnwrapID(MessageReceivedEventArgs e)
        {
            ushort id = 0;
            using (Message message = e.GetMessage())
            using (DarkRiftReader reader = message.GetReader())
            {
                id = reader.ReadUInt16();
            }

            return id;
        }

        private void PlayAttackPlayer(MessageReceivedEventArgs e)
        {
            ushort networkID = UnwrapID(e);
            SendNetworkID(networkID, ServerMessage.PlayAttackPlayer, SendMode.Unreliable, e.Client.ID);
            players.SpaghettiAttack(networkID);
        }

        private void AttackStarted(GameObject gameObject, ObjectType objectType, object p2, object p3)
        {
            ushort networkID = players.GetNetworkID(gameObject);
            SendNetworkID(networkID, ServerMessage.PlayAttackPlayer, SendMode.Unreliable);
        }

        /// <summary>
        /// Handles start request and sends message to all clients.
        /// </summary>
        /// <param name="p0">The parameter is not used.</param>
        /// <param name="p1">The parameter is not used.</param>
        /// <param name="p2">The parameter is not used.</param>
        /// <param name="p3">The parameter is not used.</param>
        private void LevelStartRequested(object p0, object p1, object p2, object p3)
        {
            this.NetworkEvents.LevelStarted.Trigger();
            using (Message message = Message.CreateEmpty(ServerMessage.LevelStart))
            {
                foreach (var client in Server.Server.ClientManager.GetAllClients())
                {
                    client.SendMessage(message, SendMode.Reliable);
                }
            }
        }

        private void UpdatePlayer(MessageReceivedEventArgs e)
        {
            using (Message message = e.GetMessage())
            {
                players.DeserializeAndUpdate(message);
                foreach (var client in Server.Server.ClientManager.GetAllClients())
                {
                    if (client.ID != e.Client.ID)
                    {
                        client.SendMessage(message, SendMode.Reliable);
                    }
                }
            }
        }

        private void EnemyCreationRequested(GameObject prefab, Vector3 position, object p2, object p3)
        {
            Debug.Log($"Requested creation of {prefab} in {position}");
            Enemy enemy = new Enemy(enemyIDPool.Next(), position, 0);
            enemies.Create(prefab, enemy, true);
        }

        private void DeleteObject(MessageReceivedEventArgs e, ObjectType objectType)
        {
            ushort id;
            using (Message message = e.GetMessage())
            using (DarkRiftReader reader = message.GetReader())
            {
                id = reader.ReadUInt16();
            }

            switch (objectType)
            {
                case ObjectType.Enemy:
                    enemies.Destroy(id);
                    break;
                case ObjectType.Player:
                    players.Destroy(id);
                    break;
                default:
                    Debug.LogError("TODO: Write error message");
                    break;
            }
        }

        private void DestroyObject(GameObject gameObject, ObjectType objectType, object p2, object p3)
        {
            // TODO: Clean up copypaste
            ushort networkID = 0;
            ushort messageTag = 0;
            switch (objectType)
            {
                case ObjectType.Enemy:
                    networkID = enemies.GetNetworkID(gameObject);
                    enemies.Destroy(networkID);
                    messageTag = ServerMessage.DeleteEnemy;
                    break;
                case ObjectType.Player:
                    networkID = players.GetNetworkID(gameObject);
                    players.Destroy(networkID);
                    messageTag = ServerMessage.DeletePlayer;
                    break;
                default:
                    Debug.LogError("TODO: Writer error message");
                    break;
            }

            SendNetworkID(networkID, messageTag, SendMode.Reliable);
        }
    }
}
