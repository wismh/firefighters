using _Project.FireHose;
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

        public override void InstallBindings()
        {
            Container.BindInterfacesAndSelfTo<PlayerInputs>().AsSingle().WithArguments(inputActions);
            Container.Bind<RoadNetwork>().FromComponentInHierarchy().AsSingle();
            Container.Bind<TrafficSystem>().FromComponentInHierarchy().AsSingle();
            ParticleFxPoolsInstaller<WaterFxId>.Install(Container, new[]
            {
                new ParticleFxPoolBinding<WaterFxId>(WaterFxId.GroundSplash, groundSplash, 16),
                new ParticleFxPoolBinding<WaterFxId>(WaterFxId.WallSplash, wallSplash, 16),
            }, transform);
        }
    }
}
