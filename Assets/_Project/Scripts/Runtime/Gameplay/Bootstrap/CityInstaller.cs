using _Project.Audio;
using _Project.City;
using _Project.Comedy;
using _Project.Economy;
using _Project.Fire;
using _Project.FireHose;
using _Project.Hazards;
using _Project.Hud;
using _Project.Mission;
using _Project.Pedestrians;
using _Project.Player;
using _Project.Traffic;
using MyToolkit.ParticleFx;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

namespace _Project.Bootstrap
{
    /// <summary>
    /// Composition root of the city scene: the only place that knows every module. Scene systems, the player and
    /// the HUD views are bound here and reach each other through [Inject]; [SerializeField] is left for a
    /// component's own children, assets and tuning.
    /// </summary>
    public sealed class CityInstaller : MonoInstaller
    {
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private DamagePrices prices;
        [Tooltip("Where one-shot sounds are heard from: the view camera.")]
        [SerializeField] private Transform listener;
        [SerializeField] private float hearingDistance = 70f;
        [SerializeField] private PooledParticleFxView groundSplash;
        [SerializeField] private PooledParticleFxView wallSplash;
        [SerializeField] private PooledParticleFxView steam;
        [SerializeField] private ComedyFxEntry[] comedyFx;

        public override void InstallBindings()
        {
            BindPlayer();
            BindCity();
            BindFire();
            BindMission();
            BindComedy();
        }

        private void BindPlayer()
        {
            Container.BindInterfacesAndSelfTo<PlayerInputs>().AsSingle().WithArguments(inputActions);
            Container.BindInterfacesAndSelfTo<HudInputs>().AsSingle().WithArguments(inputActions);
            Container.BindInterfacesAndSelfTo<CursorLock>().AsSingle();
            Container.BindInterfacesAndSelfTo<RunawayCar>().AsSingle();
            Container.BindInterfacesAndSelfTo<PlayerModeController>().FromComponentInHierarchy().AsSingle();
            Container.Bind<FirstPersonController>().FromComponentInHierarchy().AsSingle();
            Container.Bind<BurningView>().FromComponentInHierarchy().AsSingle();
        }

        private void BindCity()
        {
            Container.Bind<RoadNetwork>().FromComponentInHierarchy().AsSingle();
            Container.Bind<TrafficSystem>().FromComponentInHierarchy().AsSingle();
            Container.BindInterfacesAndSelfTo<PedestrianSystem>().FromComponentInHierarchy().AsSingle();
            Container.Bind<KnockableProp>().FromComponentsInHierarchy().AsTransient();
            Container.Bind<HydrantGeysers>().FromComponentInHierarchy().AsSingle();
            Container.Bind<SpatialSfx>().AsSingle().WithArguments(listener, hearingDistance);
        }

        private void BindFire()
        {
            ParticleFxPoolsInstaller<WaterFxId>.Install(Container, new[]
            {
                new ParticleFxPoolBinding<WaterFxId>(WaterFxId.GroundSplash, groundSplash, 16),
                new ParticleFxPoolBinding<WaterFxId>(WaterFxId.WallSplash, wallSplash, 16),
                new ParticleFxPoolBinding<WaterFxId>(WaterFxId.Steam, steam, 12),
            }, transform);
            Container.Bind<WaterNozzle>().FromComponentInHierarchy().AsSingle();
            Container.Bind<BuildingFire>().FromComponentsInHierarchy().AsTransient();
            Container.BindInterfacesAndSelfTo<FireService>().AsSingle();
        }

        private void BindMission()
        {
            Container.Bind<DamageBill>().AsSingle().WithArguments(prices);
            Container.Bind<MissionDirector>().FromComponentInHierarchy().AsSingle();
            Container.Bind<RadioView>().FromComponentInHierarchy().AsSingle();
            Container.Bind<TargetIndicator>().FromComponentInHierarchy().AsSingle();
        }

        private void BindComedy()
        {
            Container.Bind<CameraFeel>().FromComponentInHierarchy().AsSingle();
            Container.Bind<FloatingTextView>().FromComponentInHierarchy().AsSingle();
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
