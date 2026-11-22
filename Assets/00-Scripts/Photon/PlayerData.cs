using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class PlayerData : NetworkBehaviour
{
    // Only contains players that are currently spawned, so their [Networked] values are safe to read.
    public static readonly List<PlayerData> All = new();

    [Networked] public NetworkString<_16> Name { get; set; } // synced text, max 16 chars
    [Networked] public int Slot { get; set; }                // 1 = host, 2 = joiner

    public override void Spawned()
    {
        All.Add(this);

        // Applies for only the owner
        if (HasInputAuthority)
        {
            RPC_SetName(PlayerPrefs.GetString("PlayerName", "Player"));
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        All.Remove(this);
    }

    // Sent by the owning player, runs on the host.
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SetName(string playerName)
    {
        Name = playerName;
    }
}
