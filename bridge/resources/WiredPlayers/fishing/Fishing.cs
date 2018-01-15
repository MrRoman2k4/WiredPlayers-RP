﻿using GTANetworkAPI;
using WiredPlayers.globals;
using WiredPlayers.model;
using WiredPlayers.faction;
using WiredPlayers.database;
using System.Collections.Generic;
using System.Threading;
using System;

namespace WiredPlayers.fishing
{
    public class Fishing : Script
    {
        private static Dictionary<int, Timer> fishingTimerList = new Dictionary<int, Timer>();

        public Fishing()
        {
            Event.OnClientEventTrigger += OnClientEventTrigger;
            Event.OnPlayerDisconnected += OnPlayerDisconnected;
        }

        private void OnClientEventTrigger(Client player, string eventName, params object[] arguments)
        {
            int playerId = 0;
            Timer fishingTimer = null;
            Random random = new Random();

            switch (eventName)
            {
                case "startFishingTimer":
                    // Obtenemos el identificador del jugador
                    playerId = NAPI.Data.GetEntityData(player, EntityData.PLAYER_ID);

                    // Creamos el timer para que piquen
                    fishingTimer = new Timer(OnFishingPrewarnTimer, player, random.Next(1250, 2500), Timeout.Infinite);
                    fishingTimerList.Add(playerId, fishingTimer);

                    // Aplicamos la animación de lanzar la caña y avisamos al jugador
                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + Messages.INF_PLAYER_FISHING_ROD_THROWN);
                    break;
                case "fishingCanceled":
                    // Obtenemos el identificador del jugador
                    playerId = NAPI.Data.GetEntityData(player, EntityData.PLAYER_ID);

                    // Cancelamos el estado de pesca del jugador
                    NAPI.Player.StopPlayerAnimation(player);
                    NAPI.Player.FreezePlayer(player, false);
                    NAPI.Data.ResetEntityData(player, EntityData.PLAYER_FISHING);

                    // Borramos el timer de la lista
                    if (fishingTimerList.TryGetValue(playerId, out fishingTimer) == true)
                    {
                        // Eliminamos el timer
                        fishingTimer.Dispose();
                        fishingTimerList.Remove(playerId);
                    }

                    // Mandamos el mensaje al jugador
                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_FISHING_CANCELED);
                    break;
                case "fishingSuccess":
                    // Calculamos la posibilidad de fallo
                    bool failed = false;
                    int successChance = random.Next(100);

                    // Obtenemos el nivel del jugador
                    int fishingLevel = getPlayerFishingLevel(player);

                    switch(fishingLevel)
                    {
                        case 1:
                            failed = successChance >= 70;
                            break;
                        case 2:
                            failed = successChance >= 80;
                            break;
                        default:
                            failed = successChance >= 90;
                            fishingLevel = 3;
                            break;
                    }

                    if(!failed)
                    {
                        // Obtenemos la ganancia del jugador
                        int fishWeight = random.Next(fishingLevel * 100, fishingLevel * 750);
                        int playerDatabaseId = NAPI.Data.GetEntityData(player, EntityData.PLAYER_SQL_ID);
                        ItemModel fishItem = Globals.getPlayerItemModelFromHash(playerDatabaseId, Constants.ITEM_HASH_FISH);

                        if(fishItem == null)
                        {
                            fishItem = new ItemModel();
                            fishItem.amount = fishWeight;
                            fishItem.hash = Constants.ITEM_HASH_FISH;
                            fishItem.ownerEntity = Constants.ITEM_ENTITY_PLAYER;
                            fishItem.ownerIdentifier = playerDatabaseId;
                            fishItem.position = new Vector3(0.0f, 0.0f, 0.0f);
                            fishItem.dimension = 0;

                            // Añadimos el objeto
                            fishItem.id = Database.addNewItem(fishItem);
                            Globals.itemList.Add(fishItem);
                        }
                        else
                        {
                            fishItem.amount += fishWeight;
                            Database.updateItem(fishItem);
                        }

                        // Mandamos el mensaje al jugador
                        String message = String.Format(Messages.INF_FISHED_WEIGHT, fishWeight);
                        NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + message);
                    }
                    else
                    {
                        NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + Messages.INF_GARBAGE_FISHED);
                    }

                    // Sumamos un punto al nivel de habilidad
                    int fishingPoints = Job.getJobPoints(player, Constants.JOB_FISHERMAN);
                    Job.setJobPoints(player, Constants.JOB_FISHERMAN, fishingPoints + 1);

                    // Cancelamos el estado de pesca del jugador
                    NAPI.Player.StopPlayerAnimation(player);
                    NAPI.Player.FreezePlayer(player, false);
                    NAPI.Data.ResetEntityData(player, EntityData.PLAYER_FISHING);

                    break;
                case "fishingFailed":
                    // Cancelamos el estado de pesca del jugador
                    NAPI.Player.StopPlayerAnimation(player);
                    NAPI.Player.FreezePlayer(player, false);
                    NAPI.Data.ResetEntityData(player, EntityData.PLAYER_FISHING);

                    // Mandamos el mensaje al jugador
                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_FISHING_FAILED);
                    break;
            }
        }

        private void OnPlayerDisconnected(Client player, byte type, string reason)
        {
            if (NAPI.Data.HasEntityData(player, EntityData.PLAYER_PLAYING) == true)
            {
                // Miramos si tiene el timer activo
                Timer fishingTimer = null;
                int playerId = NAPI.Data.GetEntityData(player, EntityData.PLAYER_ID);

                if (fishingTimerList.TryGetValue(playerId, out fishingTimer) == true)
                {
                    // Eliminamos el timer
                    fishingTimer.Dispose();
                    fishingTimerList.Remove(playerId);
                }
            }
        }

        private void OnFishingPrewarnTimer(object playerObject)
        {
            try
            {
                Timer fishingTimer = null;
                Client player = (Client)playerObject;
                int playerId = NAPI.Data.GetEntityData(player, EntityData.PLAYER_ID);

                // Mandamos el mensaje y aplicamos la animación
                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + Messages.INF_SOMETHING_BAITED);
                NAPI.Player.PlayPlayerAnimation(player, (int)Constants.AnimationFlags.Loop, "amb@world_human_stand_fishing@idle_a", "idle_c");

                // Borramos el timer de la lista
                if (fishingTimerList.TryGetValue(playerId, out fishingTimer) == true)
                {
                    // Eliminamos el timer
                    fishingTimer.Dispose();
                    fishingTimerList.Remove(playerId);
                }

                // Empezamos el minijuego
                NAPI.ClientEvent.TriggerClientEvent(player, "fishingBaitTaken");
            }
            catch (Exception ex)
            {
                NAPI.Util.ConsoleOutput("[EXCEPTION OnFishingPrewarnTimer] " + ex.Message);
            }
        }

        private int getPlayerFishingLevel(Client player)
        {
            // Obtenemos los puntos del jugador
            int fishingPoints = Job.getJobPoints(player, Constants.JOB_FISHERMAN);

            // Calculamos el nivel
            if (fishingPoints > 600) return 5;
            if (fishingPoints > 300) return 4;
            if (fishingPoints > 150) return 3;
            if (fishingPoints > 50) return 2;
            return 1;
        }

        [Command("pescar")]
        public void pescarCommand(Client player)
        {
            if(NAPI.Data.HasEntityData(player, EntityData.PLAYER_FISHING) == true)
            {
                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_PLAYER_ALREADY_FISHING);
            }
            else if(NAPI.Player.GetPlayerVehicleSeat(player) == Constants.VEHICLE_SEAT_DRIVER)
            {
                NetHandle vehicle = NAPI.Player.GetPlayerVehicle(player);
                VehicleHash vehicleModel = (VehicleHash)NAPI.Entity.GetEntityModel(vehicle);
                if(vehicleModel == VehicleHash.Marquis || vehicleModel == VehicleHash.Tug)
                {
                    if (NAPI.Data.GetEntityData(player, EntityData.PLAYER_JOB) != Constants.JOB_FISHERMAN)
                    {
                        NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_PLAYER_NOT_FISHERMAN);
                    }
                    else if(NAPI.Data.HasEntityData(player, EntityData.PLAYER_FISHABLE) == false)
                    {
                        NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_NOT_FISHING_ZONE);
                    }
                    else
                    {

                    }
                }
                else
                {
                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_NOT_FISHING_BOAT);
                }
            }
            else if(NAPI.Data.HasEntityData(player, EntityData.PLAYER_RIGHT_HAND) == true)
            {
                int fishingRodId = NAPI.Data.GetEntityData(player, EntityData.PLAYER_RIGHT_HAND);
                ItemModel fishingRod = Globals.getItemModelFromId(fishingRodId);

                if(fishingRod != null && fishingRod.hash == Constants.ITEM_HASH_FISHING_ROD)
                {
                    int playerId = NAPI.Data.GetEntityData(player, EntityData.PLAYER_SQL_ID);
                    ItemModel bait = Globals.getPlayerItemModelFromHash(playerId, Constants.ITEM_HASH_BAIT);
                    if(bait != null && bait.amount > 0)
                    {
                        foreach(Vector3 fishingPosition in Constants.FISHING_POSITION_LIST)
                        {
                            // Ponemos al jugador mirando al mar
                            NAPI.Entity.SetEntityRotation(player, new Vector3(0.0f, 0.0f, 142.532f));
                            NAPI.Player.FreezePlayer(player, true);

                            // Le quitamos una unidad de cebo
                            if(bait.amount == 1)
                            {
                                Globals.itemList.Remove(bait);
                                Database.removeItem(bait.id);
                            }
                            else
                            {
                                bait.amount--;
                                Database.updateItem(bait);
                            }

                            // Iniciamos la pesca
                            NAPI.Player.PlayPlayerAnimation(player, (int)Constants.AnimationFlags.Loop, "amb@world_human_stand_fishing@base", "base");                            
                            NAPI.Data.SetEntityData(player, EntityData.PLAYER_FISHING, true);
                            NAPI.ClientEvent.TriggerClientEvent(player, "startPlayerFishing");
                            return;
                        }
                        
                        // Avisamos de que no se encuentra en la zona de pesca
                        NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_PLAYER_NOT_FISHING_ZONE);
                    }
                    else
                    {
                        NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_PLAYER_NO_BAITS);
                    }
                }
                else
                {
                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_NO_FISHING_ROD);
                }
            }
            else
            {
                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_PLAYER_NOT_ROD_BOAT);
            }
        }
    }
}