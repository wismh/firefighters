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

        [Header("Directions")]
        [Tooltip("Where the fire is from the player, clockwise from north in eighths.")]
        public string[] directions =
        {
            "на півночі", "на північному сході", "на сході", "на південному сході",
            "на півдні", "на південному заході", "на заході", "на північному заході",
        };
    }
}
