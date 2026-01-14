using System;
using UnityEngine;

[CreateAssetMenu(menuName = "HexaCastle/Wave Plan", fileName = "WavePlan_")]
public sealed class WavePlan : ScriptableObject
{
    public RoundDef[] rounds;

    public bool TryGetRound(int waveNumber, out RoundDef round)
    {
        round = null;
        if (rounds == null) return false;

        for (int i = 0; i < rounds.Length; i++)
        {
            var r = rounds[i];
            if (r != null && r.waveNumber == waveNumber)
            {
                round = r;
                return true;
            }
        }
        return false;
    }

    [Serializable]
    public sealed class RoundDef
    {
        [Min(1)] public int waveNumber = 1;
        public SubWaveDef[] subWaves;
    }

    [Serializable]
    public sealed class SubWaveDef
    {
        [Min(0f)] public float delayFromPrevious = 0f;
        [Min(0.01f)] public float spawnInterval = 0.5f;
        public EnemyPack[] packs;
    }

    [Serializable]
    public sealed class EnemyPack
    {
        public EnemyType type;
        [Min(1)] public int count = 1;
    }
}
