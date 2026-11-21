using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using MyToolkit.StateMachine;
using NUnit.Framework;

namespace MyToolkit.Tests.StateMachine
{
    public sealed class StateMachineTests
    {
        private sealed class TestMachine : StateMachineBehaviour<StateBase>
        {
        }

        private abstract class LoggedState : StateBase
        {
            protected LoggedState(List<string> log) => Log = log;

            protected List<string> Log { get; }

            public override UniTask Enter()
            {
                Log.Add(GetType().Name + ".Enter");
                return UniTask.CompletedTask;
            }

            public override UniTask Exit()
            {
                Log.Add(GetType().Name + ".Exit");
                return UniTask.CompletedTask;
            }
        }

        private sealed class StateA : LoggedState
        {
            public StateA(List<string> log) : base(log) { }
        }

        private sealed class StateB : LoggedState
        {
            public StateB(List<string> log) : base(log) { }
        }

        private sealed class NotRegistered : LoggedState
        {
            public NotRegistered(List<string> log) : base(log) { }
        }

        private sealed class PayloadState : PayLoadedStateBase<int>
        {
            private readonly List<string> _log;

            public PayloadState(List<string> log) => _log = log;

            public override UniTask Enter() => UniTask.CompletedTask;

            public override UniTask Enter(int payLoad)
            {
                _log.Add("PayloadState.Enter(" + payLoad + ")");
                return UniTask.CompletedTask;
            }

            public override UniTask Exit()
            {
                _log.Add("PayloadState.Exit");
                return UniTask.CompletedTask;
            }
        }

        private List<string> _log;
        private TestMachine _machine;

        [SetUp]
        public void SetUp()
        {
            _log = new List<string>();
            _machine = new TestMachine();
            _machine.SetStates(new List<StateBase> { new StateA(_log), new StateB(_log), new PayloadState(_log) });
        }

        [Test]
        public void Enter_ExitsPreviousStateBeforeEnteringNext()
        {
            _machine.Enter<StateA>().GetAwaiter().GetResult();
            _machine.Enter<StateB>().GetAwaiter().GetResult();

            Assert.AreEqual(new[] { "StateA.Enter", "StateA.Exit", "StateB.Enter" }, _log);
            Assert.AreEqual(typeof(StateB), _machine.GetCurrentState());
        }

        [Test]
        public void Enter_UnregisteredState_ThrowsAndKeepsCurrentState()
        {
            _machine.Enter<StateA>().GetAwaiter().GetResult();
            _log.Clear();

            Assert.Throws<InvalidOperationException>(
                () => _machine.Enter<NotRegistered>().GetAwaiter().GetResult());

            Assert.IsEmpty(_log);
            Assert.AreEqual(typeof(StateA), _machine.GetCurrentState());
        }

        [Test]
        public void EnterWithPayload_PassesPayloadAndExitsPrevious()
        {
            _machine.Enter<StateA>().GetAwaiter().GetResult();
            _machine.Enter<PayloadState, int>(7).GetAwaiter().GetResult();

            Assert.AreEqual(
                new[] { "StateA.Enter", "StateA.Exit", "PayloadState.Enter(7)" },
                _log);
            Assert.AreEqual(typeof(PayloadState), _machine.GetCurrentState());
        }

        [Test]
        public void EnterWithPayload_UnregisteredState_KeepsCurrentState()
        {
            var machine = new TestMachine();
            machine.SetStates(new List<StateBase> { new StateA(_log) });
            machine.Enter<StateA>().GetAwaiter().GetResult();
            _log.Clear();

            Assert.Throws<InvalidOperationException>(
                () => machine.Enter<PayloadState, int>(1).GetAwaiter().GetResult());

            Assert.IsEmpty(_log);
            Assert.AreEqual(typeof(StateA), machine.GetCurrentState());
        }

        [Test]
        public void ShutdownAsync_ExitsActiveState_AndBlocksFurtherUse()
        {
            _machine.Enter<StateA>().GetAwaiter().GetResult();

            _machine.ShutdownAsync().GetAwaiter().GetResult();

            Assert.Contains("StateA.Exit", _log);
            Assert.Throws<ObjectDisposedException>(
                () => _machine.Enter<StateB>().GetAwaiter().GetResult());
        }
    }
}
