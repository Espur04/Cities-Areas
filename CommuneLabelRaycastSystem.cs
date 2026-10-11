using System.Reflection;
using Colossal.Serialization.Entities;
using Game;
using Game.Areas;
using Game.Common;
using Game.SceneFlow;
using Game.Tools;
using Unity.Entities;

namespace Communes
{

public partial class CommuneLabelRaycastSystem : GameSystemBase
    {
        private static readonly FieldInfo kLabelQueryField = typeof(RaycastSystem).GetField(
            "m_LabelQuery", BindingFlags.Instance | BindingFlags.NonPublic);

        private RaycastSystem m_RaycastSystem;
        private EntityQuery m_OriginalQuery;
        private EntityQuery m_MergedQuery;
        private bool m_Patched;
        private bool m_RepatchAfterLoad;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_RaycastSystem = World.GetOrCreateSystemManaged<RaycastSystem>();
            if (kLabelQueryField == null)
            {
                Mod.log.Error("[Communes][Raycast] Champ RaycastSystem.m_LabelQuery introuvable.");
                return;
            }

            PatchLabelQuery();
        }

        private void PatchLabelQuery()
        {
            if (kLabelQueryField == null || m_RaycastSystem == null)
                return;

            EntityQuery current = (EntityQuery)kLabelQueryField.GetValue(m_RaycastSystem);
            if (m_Patched && current.Equals(m_MergedQuery))
                return;

if (!m_Patched || !current.Equals(m_MergedQuery))
                m_OriginalQuery = current;

            m_MergedQuery = GetEntityQuery(
                new EntityQueryDesc
                {
                    All = new[] { ComponentType.ReadOnly<District>() },
                    None = new[] { ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Temp>() }
                },
                new EntityQueryDesc
                {
                    All = new[]
                    {
                        ComponentType.ReadOnly<Commune>(), ComponentType.ReadOnly<Geometry>(),
                        ComponentType.ReadOnly<LabelExtents>()
                    },

None = new[] { ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Temp>() }
                });

            kLabelQueryField.SetValue(m_RaycastSystem, m_MergedQuery);
            m_Patched = true;
            Mod.log.Info("[Communes][Raycast] Requête des labels natifs étendue à Commune.");
        }

        protected override void OnGameLoaded(Context serializationContext)
        {
            base.OnGameLoaded(serializationContext);
            m_RepatchAfterLoad = true;
        }

        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);
            m_RepatchAfterLoad = true;
        }

        protected override void OnUpdate()
        {
            if (m_RepatchAfterLoad)
            {
                PatchLabelQuery();
                m_RepatchAfterLoad = false;
            }
            else if (m_Patched)
            {

EntityQuery current = (EntityQuery)kLabelQueryField.GetValue(m_RaycastSystem);
                if (!current.Equals(m_MergedQuery))
                    PatchLabelQuery();
            }
        }

        protected override void OnDestroy()
        {
            if (m_Patched && m_RaycastSystem != null)
                kLabelQueryField.SetValue(m_RaycastSystem, m_OriginalQuery);
            base.OnDestroy();
        }
    }
}
