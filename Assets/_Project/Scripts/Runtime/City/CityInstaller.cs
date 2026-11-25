using _Project.Comedy;
using _Project.FireHose;
using _Project.Pedestrians;
using _Project.Player;
using _Project.Traffic;
using MyToolkit.ParticleFx;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

namespace _Project.City
{
    public sealed class CityInstaller : MonoInstaller
    {
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private PooledParticleFxView groundSplash;
        [SerializeField] private PooledParticleFxView wallSplash;
        [SerializeField] private PooledParticleFxView steam;
        [SerializeField] private ComedyFxEntry[] comedyFx;

        public override void InstallBindings()
        {
            Container.BindInterfacesAndSelfTo<PlayerInputs>().AsSingle().WithArguments(inputActions);
            Container.Bind<RoadNetwork>().FromComponentInHierarchy().AsSingle();
            Container.Bind<TrafficSystem>().FromComponentInHierarchy().AsSingle();
            ParticleFxPoolsInstaller<WaterFxId>.Install(Container, new[]
            {
                new ParticleFxPoolBinding<WaterFxId>(WaterFxId.GroundSplash, groundSplash, 16),
                new ParticleFxPoolBinding<WaterFxId>(WaterFxId.WallSplash, wallSplash, 16),
                new ParticleFxPoolBinding<WaterFxId>(WaterFxId.Steam, steam, 12),
            }, transform);
            Container.Bind<WaterNozzle>().FromComponentInHierarchy().AsSingle();
            Container.Bind<BuildingFire>().FromComponentsInHierarchy().AsTransient();
            Container.BindInterfacesAndSelfTo<FireService>().AsSingle();
            Container.Bind<PedestrianSystem>().FromComponentInHierarchy().AsSingle();
            Container.Bind<KnockableProp>().FromComponentsInHierarchy().AsTransient();

            var comedy = new ParticleFxPoolBinding<ComedyFxId>[comedyFx.Length];
            for (var i = 0; i < comedyFx.Length; i++)
            {
                var entry = comedyFx[i];
                comedy[i] = new ParticleFxPoolBinding<ComedyFxId>(entry.id, entry.prefab, entry.poolSize);
            }

            ParticleFxPoolsInstaller<ComedyFxId>.Install(Container, comedy, transform);
        }
    }
}
