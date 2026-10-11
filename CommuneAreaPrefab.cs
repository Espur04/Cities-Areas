using Game.Areas;
using Game.Prefabs;
using Colossal.Serialization.Entities;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace Communes
{

public class CommuneAreaPrefab : AreaPrefab
    {
        public CommuneAreaPrefab()
        {

m_Color = new Color(1f, 0f, 0f, 0.16f);
            m_EdgeColor = Color.red;
            m_SelectionColor = new Color(1f, 0.12f, 0.12f, 0.34f);
            m_SelectionEdgeColor = new Color(1f, 0.3f, 0.3f, 1f);
        }

        public static AreaColorData GetCommuneColorData() => new AreaColorData
        {
            m_FillColor = new Color32(255, 0, 0, 42),
            m_EdgeColor = new Color32(255, 0, 0, 255),
            m_SelectionFillColor = new Color32(255, 31, 31, 87),
            m_SelectionEdgeColor = new Color32(255, 76, 76, 255),
        };

        public override void GetPrefabComponents(HashSet<ComponentType> components)
        {
            base.GetPrefabComponents(components);
            components.Add(ComponentType.ReadWrite<AreaGeometryData>());
            components.Add(ComponentType.ReadWrite<AreaNameData>());

components.Add(ComponentType.ReadWrite<AreaColorData>());

}

        public override void GetArchetypeComponents(HashSet<ComponentType> components)
        {
            base.GetArchetypeComponents(components);
            components.Add(ComponentType.ReadWrite<Geometry>());
            components.Add(ComponentType.ReadWrite<LabelExtents>());
            components.Add(ComponentType.ReadWrite<LabelVertex>());
            components.Add(ComponentType.ReadWrite<Commune>());
            components.Add(ComponentType.ReadWrite<CommuneIdentity>());
            components.Add(ComponentType.ReadWrite<CommuneBuildingMember>());
        }

        public override void Initialize(EntityManager entityManager, Entity entity)
        {
            base.Initialize(entityManager, entity);

            AreaGeometryData geometry = entityManager.GetComponentData<AreaGeometryData>(entity);
            geometry.m_Type = AreaType.None;
            geometry.m_Flags = default;
            geometry.m_Flags &= ~GeometryFlags.ClearArea;

geometry.m_SnapDistance = AreaUtils.GetMinNodeDistance(AreaType.District);
            geometry.m_MaxHeight = 0f;
            geometry.m_LodBias = 0f;
            entityManager.SetComponentData(entity, geometry);

entityManager.SetComponentData(entity, new AreaNameData
            {
                m_Color = new Color32(255, 255, 255, 255),
                m_SelectedColor = new Color32(255, 150, 150, 255)
            });

            if (entityManager.HasComponent<AreaColorData>(entity))
            {
                entityManager.SetComponentData(entity, GetCommuneColorData());
            }
        }
    }

public struct Commune : IComponentData, IEmptySerializable
    {
    }

    public struct CommuneIdentity : IComponentData, ISerializable
    {
        public int m_Id;
        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter => writer.Write(m_Id);
        public void Deserialize<TReader>(TReader reader) where TReader : IReader => reader.Read(out m_Id);
    }
}
