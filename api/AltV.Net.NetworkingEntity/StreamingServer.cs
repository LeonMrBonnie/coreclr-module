﻿using System;
using System.Threading.Tasks;
using AltV.Net.Elements.Entities;
using Entity;
using Google.Protobuf;
using WebSocket = net.vieapps.Components.WebSockets.WebSocket;

namespace AltV.Net.NetworkingEntity
{
    //TODO: synchronize events via channel, also the event for transferring all entities to keep order, but for speed we probably need a channel for each player
    //TODO: verify if the synchronization of the entity storage is enough to verify order of (entity delete) / (entity add) and (get all entities) on connect 
    //TODO: add data change events
    //TODO: add position change events
    //TODO: sync entity data only to near players on change ect and only send entity data when entering streaming range
    //TODO: regenerate tokens for both players when two are trying to use same token
    
    //TODO: we can extend the managed websocket and store the player reference i think
    
    //TODO: maybe dont use IPlayer so we can host streaming server optional on different server as well,
    //TODO: but then we need communication way between game server and streaming server to send token to player or allow sending it
    //TODO: via own abstraction as a callback everyone can implement with own method
    //TODO: so that authentication verification can be handled by own tokens ect.
    public class StreamingServer
    {
        private readonly WebSocket webSocket;

        private readonly AuthProvider authProvider = new AuthProvider();

        private readonly WebSocketRepository webSocketRepository = new WebSocketRepository();

        private readonly EntityIdStorage entityIdStorage = new EntityIdStorage();

        private readonly EntityRepository entityRepository = new EntityRepository();
        
        public event Action<Entity.Entity, IPlayer> EntityStreamInHandler;

        public event Action<Entity.Entity, IPlayer> EntityStreamOutHandler;

        public StreamingServer()
        {
            entityRepository.OnEntityAdd += entity =>
            {
                var createEvent = new ServerEvent {Create = {Entity = entity}};
                webSocketRepository.SendToAll(createEvent);
            };
            entityRepository.OnEntityRemove += id =>
            {
                var deleteEvent = new ServerEvent {Delete = {Id = id}};
                webSocketRepository.SendToAll(deleteEvent);
            };
            entityRepository.OnEntityPositionUpdate += (id, position) =>
            {
                var deleteEvent = new ServerEvent {PositionChange = {Id = id, Position = position}};
                webSocketRepository.SendToAll(deleteEvent);
            };
            entityRepository.OnEntityDataUpdate += (id, key, value) =>
            {
                var dataChangeEvent = new ServerEvent {DataChange = {Id = id, Key = key, Value = value}};
                var streamedInPlayers = entityRepository.GetStreamedInPlayers(id);
                webSocketRepository.SendToPlayers(streamedInPlayers.GetEnumerator(), dataChangeEvent);
            };
            entityRepository.OnEntityStreamIn += (entity, player) =>
            {
                //TODO: send entity data to player, maybe create a snapshot version and check if player already knows the entity data
                //TODO: snapshot version needs to reset to 0 and transfers data to entity when stream in even when it doesnt change for consistency
                EntityStreamInHandler?.Invoke(entity, player);
            };
            entityRepository.OnEntityStreamOut += (entity, player) =>
            {
                //TODO: forward this event to AltNetworking because here is no use case for it, but forward the stream in as well
                EntityStreamOutHandler?.Invoke(entity, player);
            };

            Alt.OnPlayerConnect += (player, reason) =>
            {
                Task.Run(() => { authProvider.SendAuthentication(player); });
            };
            Alt.OnPlayerRemove += player =>
            {
                Task.Run(async () =>
                {
                    var task = webSocketRepository.Remove(player, webSocket);
                    if (task != null)
                    {
                        await task;
                    }

                    authProvider.RemoveAuthentication(player);
                });
            };
            webSocket = new WebSocket
            {
                OnError = (webSocket, exception) =>
                {
                    // your code to handle error
                },
                OnConnectionEstablished = (webSocket) =>
                {
                    // your code to handle established connection
                },
                OnConnectionBroken = (webSocket) =>
                {
                    // your code to handle broken connection
                },
                OnMessageReceived = (webSocket, result, data) =>
                {
                    Task.Run(() =>
                    {
                        var clientEvent = ClientEvent.Parser.ParseFrom(data);
                        if (clientEvent == null) return;
                        var authEvent = clientEvent.Auth;
                        var streamIn = clientEvent.StreamIn;
                        var streamOut = clientEvent.StreamOut;
                        if (authEvent != null)
                        {
                            var token = authEvent.Token;
                            if (token == null) return;
                            var player = authProvider.VerifyAuthentication(token);
                            lock (player)
                            {
                                if (player.Exists)
                                {
                                    webSocketRepository.Add(player, webSocket);
                                }

                                //players.Remove(token); //TODO: keep token alive so we can reconnect on connection lost
                            }

                            var sendEvent = new ServerEvent();
                            var currSendEvent = new EntitySendEvent();
                            lock (entityRepository.Entities)
                            {
                                currSendEvent.Entities.Add(entityRepository.GetAll());
                                sendEvent.Send = currSendEvent;
                                webSocket.SendAsync(sendEvent.ToByteArray(), false);
                            }
                        }
                        else if (streamIn != null)
                        {
                            var player = webSocketRepository.GetPlayer(webSocket);
                            if (player == null) return;
                            entityRepository.StreamedIn(player, streamIn.EntityId);
                            if (entityRepository.DoesPlayerNeedsNewData(streamIn.EntityId, player))
                            {
                                //var dataChangeEvent = new ServerEvent {DataChange = {Id = id, Key = key, Value = value}};
                                //webSocket.SendAsync(dataChangeEvent.ToByteArray(), false);
                            }
                        }
                        else if (streamOut != null)
                        {
                            var player = webSocketRepository.GetPlayer(webSocket);
                            if (player == null) return;
                            entityRepository.StreamedOut(player, streamOut.EntityId);
                        }
                    });
                }
            };
            webSocket.StartListen();
        }

        public void CreateEntity(Entity.Entity entity)
        {
            var id = entityIdStorage.GetNext();
            entity.Id = id;
            entityRepository.Add(entity);
        }

        public void DeleteEntity(ulong id)
        {
            entityIdStorage.Free(id);
            entityRepository.Delete(id);
        }

        public void UpdateEntityPosition(ulong id, Position position)
        {
            entityRepository.UpdatePosition(id, position);
        }

        public void UpdateEntityData(ulong id, string key, MValue value)
        {
            entityRepository.UpdateData(id, key, value);
        }
    }
}