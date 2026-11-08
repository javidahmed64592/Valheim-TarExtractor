using System;
using System.Collections.Generic;

using UnityEngine;

namespace TarExtractorMod;

/// <summary>
/// Replaces the vanilla SapCollector logic on the cloned Sap Extractor.
/// Produces 1 Tar every N seconds (default 60), up to a maximum (default 200).
/// State lives in the ZDO so it survives logout and is shared between players.
/// </summary>
public class TarExtractor : MonoBehaviour, Hoverable, Interactable
{
    private const string LevelKey = "TarExtractor_level";
    private const string LastTickKey = "TarExtractor_lastTick";
    private const string ExtractRpc = "TarExtractor_Extract";
    private const string ResultRpc = "TarExtractor_Result";

    /// <summary>Every placed extractor that is loaded (not placement ghosts).</summary>
    private static readonly List<TarExtractor> Instances = [];

    private ZNetView _nview;
    private GameObject _notEmptyEffect;

    private void Awake()
    {
        _nview = GetComponent<ZNetView>();

        // The placement ghost has a ZNetView that is never initialised - do nothing there.
        if (_nview == null || !_nview.IsValid())
        {
            return;
        }

        Instances.Add(this);
        _nview.Register(ExtractRpc, RPC_Extract);
        _nview.Register<bool>(ResultRpc, RPC_Result);

        Transform notEmpty = TarExtractorPlugin.FindChild(transform, TarExtractorPlugin.NotEmptyEffectName);
        _notEmptyEffect = notEmpty != null ? notEmpty.gameObject : null;

        InvokeRepeating(nameof(UpdateTick), UnityEngine.Random.Range(0f, 2f), 2f);
        // Every client reads the synced level, so all players see the effect.
        InvokeRepeating(nameof(UpdateEffects), 0f, 1f);
    }

    private void OnDestroy()
    {
        _ = Instances.Remove(this);
    }

    /// <summary>How many loaded extractors stand in the tar pit.</summary>
    internal static int CountIn(Location pit)
    {
        int count = 0;
        foreach (TarExtractor extractor in Instances)
        {
            if (extractor != null && TarPit.Contains(pit, extractor.transform.position))
            {
                count++;
            }
        }
        return count;
    }

    private void UpdateEffects()
    {
        if (_notEmptyEffect == null || !_nview.IsValid())
        {
            return;
        }

        bool hasTar = _nview.GetZDO().GetInt(LevelKey, 0) > 0;
        if (_notEmptyEffect.activeSelf != hasTar)
        {
            _notEmptyEffect.SetActive(hasTar);
        }
    }

    private void UpdateTick()
    {
        if (!_nview.IsValid() || !_nview.IsOwner())
        {
            return;
        }

        ZDO zdo = _nview.GetZDO();
        long now = ZNet.instance.GetTime().Ticks;
        long last = zdo.GetLong(LastTickKey, 0L);
        int level = zdo.GetInt(LevelKey, 0);
        int max = TarExtractorPlugin._maxTar.Value;

        if (level > max)
        {
            level = max;
            zdo.Set(LevelKey, level);
        }

        // First tick, or full: don't bank time while full, so the next Tar takes a full interval.
        if (last == 0L || level >= max)
        {
            zdo.Set(LastTickKey, now);
            return;
        }

        long ticksPerTar = (long)(Mathf.Max(1f, TarExtractorPlugin._secondsPerTar.Value) * TimeSpan.TicksPerSecond);
        long gained = (now - last) / ticksPerTar;
        if (gained <= 0)
        {
            return;
        }

        int newLevel = (int)Math.Min(max, level + gained);
        zdo.Set(LevelKey, newLevel);
        // Keep the remainder so partial progress isn't lost.
        zdo.Set(LastTickKey, newLevel >= max ? now : last + (gained * ticksPerTar));
    }

    public bool Interact(Humanoid user, bool hold, bool alt)
    {
        if (hold || _nview == null || !_nview.IsValid())
        {
            return false;
        }

        // Only the owner of the extractor's data empties it, like the vanilla Sap Extractor, so two players
        // collecting at the same moment can't both get the Tar.
        _nview.InvokeRPC(ExtractRpc);
        return true;
    }

    private void RPC_Extract(long sender)
    {
        if (!_nview.IsValid() || !_nview.IsOwner())
        {
            return;
        }

        UpdateTick();

        ZDO zdo = _nview.GetZDO();
        int amount = zdo.GetInt(LevelKey, 0);
        if (amount > 0)
        {
            zdo.Set(LevelKey, 0);
            zdo.Set(LastTickKey, ZNet.instance.GetTime().Ticks);
            UpdateEffects();
            SpawnTar(amount);
        }

        _nview.InvokeRPC(sender, ResultRpc, amount > 0);
    }

    /// <summary>Tells the player who collected what happened.</summary>
    private void RPC_Result(long sender, bool extracted)
    {
        if (Player.m_localPlayer != null)
        {
            Player.m_localPlayer.Message(MessageHud.MessageType.Center,
                extracted ? "$piece_tarextractor_extracted" : "$piece_tarextractor_empty");
        }
    }

    public bool UseItem(Humanoid user, ItemDrop.ItemData item)
    {
        return false;
    }

    public string GetHoverText()
    {
        if (_nview == null || !_nview.IsValid())
        {
            return string.Empty;
        }

        int level = _nview.GetZDO().GetInt(LevelKey, 0);
        int max = TarExtractorPlugin._maxTar.Value;
        return Localization.instance.Localize(
            $"$piece_tarextractor ({level}/{max})\n" +
            "[<color=yellow><b>$KEY_Use</b></color>] $piece_tarextractor_extract");
    }

    public string GetHoverName()
    {
        return Localization.instance.Localize("$piece_tarextractor");
    }

    /// <summary>
    /// Added to Hoverable in Valheim 1.0: lets a piece shift where its hover text is anchored.
    /// </summary>
    public float GetHoverOffset()
    {
        return 0.0f;
    }

    /// <summary>
    /// Drops Tar next to the extractor in stacks, like the vanilla Sap Extractor drops Sap.
    /// </summary>
    private void SpawnTar(int amount)
    {
        GameObject prefab = ObjectDB.instance.GetItemPrefab("Tar");
        if (prefab == null)
        {
            TarExtractorPlugin._log.LogError("Could not find the 'Tar' item prefab.");
            return;
        }

        int maxStack = Mathf.Max(1, prefab.GetComponent<ItemDrop>().m_itemData.m_shared.m_maxStackSize);
        Vector3 origin = transform.position + (Vector3.up * 1.2f) + (transform.forward * 0.8f);

        while (amount > 0)
        {
            int stack = Mathf.Min(amount, maxStack);
            Vector3 pos = origin + (UnityEngine.Random.insideUnitSphere * 0.25f);

            GameObject drop = Instantiate(prefab, pos, Quaternion.identity);
            drop.GetComponent<ItemDrop>().SetStack(stack);

            Rigidbody body = drop.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.linearVelocity = Vector3.up * 2f;
            }

            amount -= stack;
        }
    }
}
