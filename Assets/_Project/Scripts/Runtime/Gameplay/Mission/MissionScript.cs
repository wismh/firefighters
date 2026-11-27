using System;
using UnityEngine;

namespace _Project.Mission
{
    /// <summary>
    /// What the dispatcher says and when: the intro, a line per call-out, hints on the first fire, reactions, and the
    /// closing words that depend on the damage bill. In call-out lines {0} is the direction and {1} the distance.
    /// </summary>
    [CreateAssetMenu(menuName = "Project/Mission Script", fileName = "MissionScript")]
    public sealed class MissionScript : ScriptableObject
    {
        /// <summary>The report's last word on the bill: the first row whose ceiling the bill does not exceed.</summary>
        [Serializable]
        public struct Verdict
        {
            public int maxBill;
            public string text;
        }

        public string speakerName = "Диспетчерка Валентина";
        public string callSign = "БАЗА";

        [Header("Timing")]
        [Tooltip("Seconds of quiet after the game starts, before the intro.")]
        public float introDelay = 1.2f;
        [Tooltip("Seconds between putting out a call and the next fire catching.")]
        public float stagePause = 5f;
        [Tooltip("Seconds of fireworks before the closing words.")]
        public float outroDelay = 4.5f;
        [Tooltip("Closer than this to a fire of the first call, metres, the player gets the hose hint.")]
        public float arrivalDistance = 45f;
        [Tooltip("A damage bill above this makes the closing words the messy ones.")]
        public int messyBill = 15000;
        [Tooltip("Seconds the report stays on screen after the closing words.")]
        public float reportSeconds = 14f;

        [Header("Intro")]
        [TextArea]
        public string[] intro =
        {
            "Новенький? Так, бачу. Ласкаво просимо до пожежної частини.",
            "Отже, твоє тестове завдання: в місті кілька пожеж. Погасиш усі, і вважай, що тебе взяли.",
            "Машина твоя, шланг на ній. Мінікарта справа вгорі, позначки на екрані підкажуть, куди їхати.",
            "І постарайся нікого не переїхати. Хоча б на першому виклику.",
        };

        [Header("Call-outs")]
        [TextArea] public string firstCall = "Перший виклик зовсім поруч: {0}, метрів {1}. Вперед!";
        [TextArea] public string nextCall = "Новий виклик: {0}, метрів {1}. Жени!";
        [TextArea] public string finalCall = "Два виклики одночасно! Це вже не тестове, це просто вівторок. Найближчий — {0}, метрів {1}.";

        [Header("First fire")]
        [TextArea] public string arrived = "Приїхав? Виходь з машини, E. Шланг на котушці ззаду, теж E.";
        [TextArea] public string hoseTaken = "ЛКМ: вода. Цілься в основу вогню, а не в небо.";
        [TextArea] public string lastFlames = "Ще трохи! Дивись на позначки, там ще жевріє.";

        [Header("Reactions")]
        [TextArea] public string reignited = "Воно знову горить. Гаси до кінця, а не до «майже».";
        [TextArea] public string pedestrianHit = "Я цього не бачила. Офіційно не бачила.";
        [TextArea]
        public string[] callSaved =
        {
            "Є! Один є. Не розслабляйся.",
            "Чудово. Тримай темп, місто ще горить.",
        };

        [Header("Outro")]
        [TextArea] public string outroStart = "Усе. Місто стоїть. Ну, переважно стоїть.";
        [TextArea] public string outroClean = "Жодної подряпини? Ти точно з нашої частини?";
        [TextArea] public string outroMessy = "Рахунок за ліхтарі, машини й нерви городян прийде окремо.";
        [TextArea] public string outroEnd = "Тестове зараховано. Завтра о сьомій, не спізнюйся.";

        [Header("HUD status ({0} the call, {1} the number of calls, {2} fires still burning)")]
        public string statusCall = "Виклик {0} / {1}";
        public string statusCallFires = "Виклик {0} / {1}   Пожеж: {2}";
        public string statusBetween = "Виклик {0} / {1} виконано. Чекай наступного...";
        public string statusDone = "Усі пожежі згашено!";

        [Header("Report")]
        public string reportTitle = "МІСТО ВРЯТОВАНО!";
        [Tooltip("{0} buildings saved.")]
        public string reportSaved = "Будинків врятовано: {0}";
        [Tooltip("{0} props broken.")]
        public string reportProps = "Ліхтарів, знаків і смітників знищено: {0}";
        [Tooltip("{0} cars knocked out, {1} people soaked, {2} pedestrians hit.")]
        public string reportCars = "Машин збито: {0}, облито: {1}, прохожих у польоті: {2}";
        [Tooltip("{0} the bill.")]
        public string reportBill = "Рахунок пожежній частині: ${0:N0}";
        public string reportPassed = "Тестове завдання: ЗАРАХОВАНО";
        public Verdict[] verdicts =
        {
            new Verdict { maxBill = 0, text = "Жодної подряпини. Ви взагалі пожежник?" },
            new Verdict { maxBill = 4999, text = "Майже акуратно. Мер навіть не помітить." },
            new Verdict { maxBill = 29999, text = "Пожежі згашено. Бюджет міста — ні." },
            new Verdict { maxBill = int.MaxValue, text = "Пожежа завдала б менше шкоди." },
        };

        [Header("Directions")]
        [Tooltip("Where the fire is from the player, clockwise from north in eighths.")]
        public string[] directions =
        {
            "на півночі", "на північному сході", "на сході", "на південному сході",
            "на півдні", "на південному заході", "на заході", "на північному заході",
        };

        public string VerdictFor(int bill)
        {
            foreach (var verdict in verdicts)
            {
                if (bill <= verdict.maxBill)
                {
                    return verdict.text;
                }
            }

            return string.Empty;
        }
    }
}
