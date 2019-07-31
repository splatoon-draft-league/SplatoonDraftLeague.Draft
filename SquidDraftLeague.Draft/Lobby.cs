﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Timers;
using SquidDraftLeague.Draft.Matchmaking;

namespace SquidDraftLeague.Draft
{
    public class Lobby
    {
        public bool IsFull => this.Players.Count == 8;

        public bool InStandby;

        public SdlPlayer Halved { get; set; }

        public DateTime StartTime { get; set; }
        public TimeSpan TimeRemaining { get; set; }
        public DateTime LastUpdate { get; private set; }
        public double LobbyPowerLevel
        {
            get
            {
                if (!this.players.Any())
                {
                    return 0;
                }

                try
                {
                    return Math.Round(
                        this.players.Where(e => e.DiscordId != this.Halved?.DiscordId).Select(e => e.PowerLevel).Average(), 2);
                }
                catch
                {
                    if (this.players.Count == 1)
                    {
                        return Math.Round(this.players.First().PowerLevel, 2);
                    }

                    return 0;
                }
            }
        }

        public SdlClass Class => Matchmaker.GetClass(this.LobbyPowerLevel);

        /// <summary>
        /// Fired when the delta has been updated. Event argument is true if the lobby was closed.
        /// </summary>
        public event EventHandler<bool> DeltaUpdated;

        public int CurrentDelta { get; private set; }
        public int LobbyNumber { get; }
        public ReadOnlyCollection<SdlPlayer> Players => this.players.AsReadOnly();

        private readonly Timer timer;

        private readonly List<SdlPlayer> players = new List<SdlPlayer>();
        private readonly List<ulong> stalePlayers = new List<ulong>();

        public Lobby(int lobbyNumber)
        {
            this.LastUpdate = DateTime.Now;

            this.LobbyNumber = lobbyNumber;
            this.CurrentDelta = 75;

            this.timer = new Timer {Interval = 300000, AutoReset = true};
            this.timer.Elapsed += this.Timer_Elapsed;
        }

        public void Close()
        {
            this.DeltaUpdated = null;
            this.Halved = null;
            this.players.Clear();
            this.stalePlayers.Clear();
            this.timer.Stop();
            this.timer.Interval = 300000;
            this.CurrentDelta = 75;
            this.InStandby = false;
        }

        public bool IsWithinThreshold(double power)
        {
            if (!this.players.Any() || Matchmaker.GetClass(power) == this.Class)
            {
                return true;
            }

            double min = this.LobbyPowerLevel - this.CurrentDelta;
            double max = this.LobbyPowerLevel + this.CurrentDelta;

            return power >= min && power <= max;
        }

        public void AddPlayer(SdlPlayer player, bool force = false)
        {
            if (!force && (this.IsFull || this.players.Any(e => e.DiscordId == player.DiscordId)))
            {
                return;
            }

            if (!this.players.Any())
            {
                this.StartTime = DateTime.Now;
                this.TimeRemaining = TimeSpan.FromMinutes(20);
            }

            this.players.Add(player);

            if (!this.stalePlayers.Contains(player.DiscordId))
            {
                switch (this.players.Count)
                {
                    case 1:
                    case 2:
                    case 3:
                    case 4:
                        this.TimeRemaining += TimeSpan.FromMinutes(5);
                        break;
                    case 5:
                    case 6:
                        this.TimeRemaining += TimeSpan.FromMinutes(10);
                        break;
                    case 7:
                        this.TimeRemaining += TimeSpan.FromMinutes(20);
                        break;
                }

                if (this.TimeRemaining > TimeSpan.FromMinutes(20))
                {
                    this.TimeRemaining = TimeSpan.FromMinutes(20);
                }

                this.stalePlayers.Add(player.DiscordId);
            }

            this.LastUpdate = DateTime.Now;
            this.timer.Stop();
            this.timer.Start();
        }

        public void RemovePlayer(SdlPlayer player)
        {
            if (this.players.Contains(player))
            {
                this.players.Remove(player);
            }

            if (this.Halved == player)
            {
                this.Halved = null;
            }

            if (this.players.Count == 0)
            {
                this.Close();
            }
            else if (!this.IsFull && !this.timer.Enabled)
            {
                this.timer.Start();
            }
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs elapsedEventArgs)
        {
            if (DateTime.Now > this.StartTime + TimeSpan.FromMinutes(40) && this.players.Count < 6)
            {
                this.DeltaUpdated?.Invoke(this, true);
                this.Close();
                return;
            }

            if (DateTime.Now >= this.LastUpdate + this.TimeRemaining)
            {
                this.DeltaUpdated?.Invoke(this, true);
                this.Close();
                return;
            }

            if (this.CurrentDelta < 125)
            {
                this.CurrentDelta += 25;
                this.DeltaUpdated?.Invoke(this, false);
            }
        }
    }
}
