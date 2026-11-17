using System;
using Project.Core.Events;
using Project.Core.Save;

namespace Project.Contexts.Application
{
    [Serializable]
    public sealed class ProgressModel
    {
        public int Version = 1;
        public int Level = 1;
        public int Coins;
    }

    public readonly struct CoinsChanged : IEvent
    {
        public readonly int Coins;
        public CoinsChanged(int coins) => Coins = coins;
    }

    /// <summary>
    /// Player progress (level, coins), saved after every change, and the tries of the current level in this session.
    /// </summary>
    public sealed class ProgressService
    {
        private const string Key = "progress";
        private readonly SaveService _save;
        private readonly ProgressModel _model;

        public ProgressService(SaveService save)
        {
            _save = save;
            _model = save.Load<ProgressModel>(Key);
        }

        public int Level => _model.Level;
        public int Coins => _model.Coins;

        /// <summary>0 for the first try of the current level, +1 for every failed or restarted try. Not saved.</summary>
        public int Attempt { get; private set; }

        /// <summary>The current try is over without a win; the next one is a retry.</summary>
        public void FailAttempt() => Attempt++;

        public void CompleteLevel(int reward)
        {
            _model.Level++;
            Attempt = 0;
            AddCoins(reward);
        }

        public void AddCoins(int amount)
        {
            _model.Coins = Math.Max(0, _model.Coins + amount);
            _save.Save(Key, _model);
            EventBus<CoinsChanged>.Raise(new CoinsChanged(_model.Coins));
        }

        public void ResetProgress()
        {
            _model.Level = 1;
            _model.Coins = 0;
            Attempt = 0;
            _save.Save(Key, _model);
            EventBus<CoinsChanged>.Raise(new CoinsChanged(0));
        }
    }
}
