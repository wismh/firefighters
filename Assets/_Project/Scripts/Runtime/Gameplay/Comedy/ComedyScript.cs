using System;
using _Project.City;
using UnityEngine;

namespace _Project.Comedy
{
    /// <summary>
    /// The words of the slapstick layer: what drivers, passers-by and the player shout, and which cartoon effect
    /// answers each kind of knocked-over prop. A line is picked at random from its list.
    /// </summary>
    [CreateAssetMenu(menuName = "Project/Comedy Script", fileName = "ComedyScript")]
    public sealed class ComedyScript : ScriptableObject
    {
        /// <summary>A cartoon effect that pops over a knocked-over prop, this high above its base.</summary>
        [Serializable]
        public struct PropFx
        {
            public PropKind kind;
            public ComedyFxId fx;
            public float height;
        }

        [Header("Drivers")]
        public string[] soaked =
        {
            "Я ж її щойно помив!", "Ей!!", "Моя зачіска!", "Це не я горю!", "Буль-буль...", "Вікно було відчинене!",
        };

        public string[] knocked = { "Мій бампер!", "Ти права купив?!", "Я дзвоню мамі!", "Страховка не покриє!", "ЩО?!" };

        [Header("Passers-by")]
        public string[] scared = { "ААААА!", "Рятуйте!", "Це ж тротуар!", "Я ще не застрахувався!", "Мамо!" };
        public string[] soakedPedestrian = { "Я не горів!", "Буль!", "Це був новий костюм!", "Душ я не замовляв!" };
        public string[] ghost = { "Моральна шкода!", "Я буду скаржитись!", "Де ти вчився водити?!", "Поверніть мене назад!" };

        [Header("Player")]
        public string[] jump = { "ГЕРОНІМО!", "Далі сама!", "Ай! Ой! Ух!", "Гальма для слабаків!" };

        public string[] burning =
        {
            "Гаряче-гаряче-гаряче!", "Я ПАЛАЮ!", "Запахло шашликом!", "Я пожежник, а не дрова!", "Мої брови!",
        };

        public string[] flight = { "ЙІІІХУУ!", "Я лечууу!", "Вище за пожежу!" };

        [Header("Shouts")]
        public string geyser = "ФОНТАН!";
        public string saved = "ВРЯТОВАНО!";
        [Tooltip("{0} is the cost.")]
        public string money = "-${0:N0}";

        [Header("Props")]
        public PropFx[] propFx =
        {
            new PropFx { kind = PropKind.Lamp, fx = ComedyFxId.MetalSparks, height = 1.2f },
            new PropFx { kind = PropKind.Lamp, fx = ComedyFxId.TextCrack, height = 6f },
            new PropFx { kind = PropKind.TrafficLight, fx = ComedyFxId.MetalSparks, height = 1.2f },
            new PropFx { kind = PropKind.TrafficLight, fx = ComedyFxId.TextZap, height = 6f },
            new PropFx { kind = PropKind.RoadSign, fx = ComedyFxId.TextPow, height = 5f },
            new PropFx { kind = PropKind.Bin, fx = ComedyFxId.Feathers, height = 0.8f },
        };

        [Tooltip("A prop this expensive or more also bursts into banknotes.")]
        public int expensiveProp = 1000;
    }
}
