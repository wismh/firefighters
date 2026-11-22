using _Project.Player;
using _Project.Traffic;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

namespace _Project.City
{
    public sealed class CityInstaller : MonoInstaller
    {
        [SerializeField] private InputActionAsset inputActions;

        public override void InstallBindings()
        {
            Container.BindInterfacesAndSelfTo<PlayerInputs>().AsSingle().WithArguments(inputActions);
            Container.Bind<RoadNetwork>().FromComponentInHierarchy().AsSingle();
            Container.Bind<TrafficSystem>().FromComponentInHierarchy().AsSingle();
        }
    }
}
