using System;
using UnityEngine;

namespace TarExtractorMod
{
    /// <summary>
    /// Replaces the vanilla SapCollector logic on the cloned Sap Extractor.
    /// Produces 1 Tar every N seconds (default 60), up to a maximum (default 200).
    /// State lives in the ZDO so it survives logout and is shared between players.
    /// </summary>
    public class TarExtractor : MonoBehaviour, Hoverable, Interactable
    {
        private const string LevelKey = "TarExtractor_level";
        private const string LastTickKey = "TarExtractor_lastTick";

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

            Transform notEmpty = TarExtractorPlugin.FindChild(transform, TarExtractorPlugin.NotEmptyEffectName);
            _notEmptyEffect = notEmpty != null ? notEmpty.gameObject : null;

            InvokeRepeating(nameof(UpdateTick), UnityEngine.Random.Range(0f, 2f), 2f);
            // Every client reads the synced level, so all players see the effect.
            InvokeRepeating(nameof(UpdateEffects), 0f, 1f);
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
            int max = TarExtractorPlugin.MaxTar.Value;

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

            long ticksPerTar = (long)(Mathf.Max(1f, TarExtractorPlugin.SecondsPerTar.Value) * TimeSpan.TicksPerSecond);
            long gained = (now - last) / ticksPerTar;
            if (gained <= 0)
            {
                return;
            }

            int newLevel = (int)Math.Min((long)max, level + gained);
            zdo.Set(LevelKey, newLevel);
            // Keep the remainder so partial progress isn't lost.
            zdo.Set(LastTickKey, newLevel >= max ? now : last + gained * ticksPerTar);
        }

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold || _nview == null || !_nview.IsValid())
            {
                return false;
            }

            // Make sure we are the one modifying the ZDO.
            _nview.ClaimOwnership();
            UpdateTick();

            ZDO zdo = _nview.GetZDO();
            int amount = zdo.GetInt(LevelKey, 0);
            if (amount <= 0)
            {
                user.Message(MessageHud.MessageType.Center, "$piece_tarextractor_empty");
                return true;
            }

            zdo.Set(LevelKey, 0);
            zdo.Set(LastTickKey, ZNet.instance.GetTime().Ticks);
            UpdateEffects();

            SpawnTar(amount);
            user.Message(MessageHud.MessageType.Center, "$piece_tarextractor_extracted");
            return true;
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
            int max = TarExtractorPlugin.MaxTar.Value;
            return Localization.instance.Localize(
                $"$piece_tarextractor ({level}/{max})\n" +
                "[<color=yellow><b>$KEY_Use</b></color>] $piece_tarextractor_extract");
        }

        public string GetHoverName()
        {
            return Localization.instance.Localize("$piece_tarextractor");
        }

        // Added to Hoverable in Valheim 1.0: lets a piece shift where its hover text is anchored.
        public float GetHoverOffset()
        {
            return 0.0f;
        }

        // Drops Tar next to the extractor in stacks, like the vanilla Sap Extractor drops Sap.
        private void SpawnTar(int amount)
        {
            GameObject prefab = ObjectDB.instance.GetItemPrefab("Tar");
            if (prefab == null)
            {
                TarExtractorPlugin.Log.LogError("Could not find the 'Tar' item prefab.");
                return;
            }

            int maxStack = Mathf.Max(1, prefab.GetComponent<ItemDrop>().m_itemData.m_shared.m_maxStackSize);
            Vector3 origin = transform.position + Vector3.up * 1.2f + transform.forward * 0.8f;

            while (amount > 0)
            {
                int stack = Mathf.Min(amount, maxStack);
                Vector3 pos = origin + UnityEngine.Random.insideUnitSphere * 0.25f;

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
}
