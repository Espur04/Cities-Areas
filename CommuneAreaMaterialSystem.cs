using System;
using System.Reflection;
using System.Collections.Generic;
using System.Collections;
using Game;
using Game.Areas;
using Game.Common;
using Game.Rendering;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using Colossal.Serialization.Entities;

namespace Communes
{

public partial class CommuneAreaMaterialSystem : GameSystemBase
    {
        private AreaBufferSystem m_AreaBufferSystem;
        private EntityQuery m_CommuneQuery;
        private FieldInfo m_SlotsField;
        private FieldInfo m_NameMaterialsField;
        private FieldInfo m_OriginalNameMaterialField;
        private readonly Dictionary<int, Material> m_OriginalMaterials = new Dictionary<int, Material>();
        private readonly Dictionary<int, int> m_CopiedFrom = new Dictionary<int, int>();
        private object m_OriginalSpaceNameMaterials;
        private Material m_OriginalSpaceNameMaterial;
        private object m_CommuneNameMaterials;
        private bool m_NameMaterialsOverridden;
        private bool m_CommuneLabelsInvalidated;
        private bool m_LoggedNameMaterials;

        public bool CommuneLabelsReady => m_NameMaterialsOverridden
            && m_CommuneNameMaterials != null && m_OriginalNameMaterialField != null;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_AreaBufferSystem = World.GetOrCreateSystemManaged<AreaBufferSystem>();
            m_CommuneQuery = GetEntityQuery(ComponentType.ReadOnly<Commune>(),
                ComponentType.ReadOnly<Area>(), ComponentType.Exclude<Deleted>());
            m_SlotsField = typeof(AreaBufferSystem).GetField("m_AreaTypeData", BindingFlags.Instance | BindingFlags.NonPublic);
            Mod.log.Info($"[Communes][Rendu] Pont matériaux/labels démarré; AreaBufferSystem={m_AreaBufferSystem != null}, champs slots trouvés={m_SlotsField != null}.");
        }

        protected override void OnGameLoaded(Context serializationContext)
        {
            base.OnGameLoaded(serializationContext);
            m_CommuneLabelsInvalidated = false;
        }

        protected override void OnUpdate()
        {
            if (m_SlotsField == null)
                return;

            Array slots = m_SlotsField.GetValue(m_AreaBufferSystem) as Array;
            if (slots == null || slots.Length <= (int)AreaType.Space)
                return;

            object districtSlot = slots.GetValue((int)AreaType.District);
            object communeSlot = slots.GetValue((int)AreaType.Space);
            if (districtSlot == null || communeSlot == null)
                return;

            Material districtFill = GetSlotMaterial(districtSlot, "m_Material");
            Material communeFill = GetSlotMaterial(communeSlot, "m_Material");
            bool hasCommunes = !m_CommuneQuery.IsEmptyIgnoreFilter;
            SyncMaterial(districtFill, communeFill, hasCommunes);

Type slotType = communeSlot.GetType();
            m_NameMaterialsField ??= slotType.GetField("m_NameMaterials", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            m_OriginalNameMaterialField ??= slotType.GetField("m_OriginalNameMaterial", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            FieldInfo districtNamesField = districtSlot.GetType().GetField("m_NameMaterials", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            FieldInfo districtOriginalNameField = districtSlot.GetType().GetField("m_OriginalNameMaterial", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            object districtNames = districtNamesField?.GetValue(districtSlot);
            Material districtOriginalName = districtOriginalNameField?.GetValue(districtSlot) as Material;

if (hasCommunes && m_NameMaterialsField != null && districtNames != null && districtOriginalName != null)
            {
                if (!m_NameMaterialsOverridden)
                {
                    m_OriginalSpaceNameMaterials = m_NameMaterialsField.GetValue(communeSlot);
                    m_OriginalSpaceNameMaterial = m_OriginalNameMaterialField?.GetValue(communeSlot) as Material;
                    m_CommuneNameMaterials = CloneNameMaterials(districtNames);
                    m_NameMaterialsOverridden = true;
                }
                m_NameMaterialsField.SetValue(communeSlot, m_CommuneNameMaterials);
                m_OriginalNameMaterialField?.SetValue(communeSlot, districtOriginalName);
                slots.SetValue(communeSlot, (int)AreaType.Space);
            }
            if (hasCommunes && m_NameMaterialsOverridden && districtNames != null && districtOriginalName != null && !m_CommuneLabelsInvalidated)
            {
                NativeArray<Entity> communes = m_CommuneQuery.ToEntityArray(Allocator.Temp);
                try
                {
                    for (int i = 0; i < communes.Length; i++)
                    {
                        if (EntityManager.HasComponent<Updated>(communes[i]))
                            EntityManager.RemoveComponent<Updated>(communes[i]);
                        EntityManager.AddComponent<Updated>(communes[i]);
                    }
                }
                finally
                {
                    communes.Dispose();
                }
                m_CommuneLabelsInvalidated = true;
            }
            else if (!hasCommunes && m_NameMaterialsOverridden)
            {
                m_NameMaterialsField?.SetValue(communeSlot, m_OriginalSpaceNameMaterials);
                m_OriginalNameMaterialField?.SetValue(communeSlot, m_OriginalSpaceNameMaterial);
                slots.SetValue(communeSlot, (int)AreaType.Space);
                m_NameMaterialsOverridden = false;
                m_CommuneNameMaterials = null;
                m_CommuneLabelsInvalidated = false;
            }

            if (!m_LoggedNameMaterials)
            {
                Mod.log.Info($"[Communes][Rendu] Support des labels actifs: matériau District présent={districtOriginalName != null}, liste District présente={districtNames != null}.");
                m_LoggedNameMaterials = true;
            }
        }

        private static Material GetSlotMaterial(object slot, string fieldName)
        {
            FieldInfo field = slot.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field?.GetValue(slot) as Material;
        }

        private int GetNameMaterialCount(object slot)
        {
            FieldInfo field = slot.GetType().GetField("m_NameMaterials", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return (field?.GetValue(slot) as System.Collections.ICollection)?.Count ?? 0;
        }

        private static object CloneNameMaterials(object source)
        {
            if (source is IList sourceList)
            {
                try
                {
                    if (Activator.CreateInstance(source.GetType()) is IList copy)
                    {
                        foreach (object item in sourceList)
                            copy.Add(item);
                        return copy;
                    }
                }
                catch { }
            }

return source;
        }

        private void SyncMaterial(Material source, Material target, bool applyDistrictStyle)
        {
            if (target == null)
                return;

            int targetId = target.GetInstanceID();
            if (!m_OriginalMaterials.ContainsKey(targetId))
                m_OriginalMaterials[targetId] = new Material(target);

            if (applyDistrictStyle && source != null)
            {
                int sourceId = source.GetInstanceID();
                if (m_CopiedFrom.TryGetValue(targetId, out int previousSource) && previousSource == sourceId)
                    return;

                target.shader = source.shader;
                target.CopyPropertiesFromMaterial(source);
                m_CopiedFrom[targetId] = sourceId;
            }
            else if (m_CopiedFrom.ContainsKey(targetId))
            {
                target.shader = m_OriginalMaterials[targetId].shader;
                target.CopyPropertiesFromMaterial(m_OriginalMaterials[targetId]);
                m_CopiedFrom.Remove(targetId);
            }
        }

        protected override void OnDestroy()
        {
            if (m_NameMaterialsOverridden && m_SlotsField?.GetValue(m_AreaBufferSystem) is Array slots
                && slots.Length > (int)AreaType.Space)
            {
                object communeSlot = slots.GetValue((int)AreaType.Space);
                m_NameMaterialsField?.SetValue(communeSlot, m_OriginalSpaceNameMaterials);
                m_OriginalNameMaterialField?.SetValue(communeSlot, m_OriginalSpaceNameMaterial);
                slots.SetValue(communeSlot, (int)AreaType.Space);
            }
            foreach (Material material in m_OriginalMaterials.Values)
            {
                if (material != null)
                    UnityEngine.Object.Destroy(material);
            }
            m_OriginalMaterials.Clear();
            m_CopiedFrom.Clear();
            base.OnDestroy();
        }
    }
}
