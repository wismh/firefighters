using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;

namespace MyToolkit.StateMachine {
    public abstract class StateMachineBehaviour<TStateBase> : IDisposable
        where TStateBase : StateBase
    {
        protected TStateBase ActiveStateBase;
        protected Dictionary<Type, TStateBase> States;

        private bool _cleanupRunning;
        private bool _disposed;
        private UniTask _exitThenClearStatesTask;

        public void SetStates(List<TStateBase> states)
        {
            ThrowIfTeardownStarted();

            States = new();
            foreach (TStateBase state in states)
            {
                States.Add(state.GetType(), state);
            }
        }

        public virtual async UniTask Enter<TState>() where TState : TStateBase
        {
            ThrowIfTeardownStarted();

            TStateBase next = ResolveState<TState>();

            if (ActiveStateBase != null)
            {
                await ActiveStateBase.Exit();
            }

            ActiveStateBase = next;
            await ActiveStateBase.Enter();
        }

        public virtual async UniTask Enter<TState, TPayload>(TPayload payLoad)
            where TState : PayLoadedStateBase<TPayload>
        {
            ThrowIfTeardownStarted();

            // Resolve before leaving the current state: a missing target must not leave the machine stateless.
            var newState = (TState)(object)ResolveState<TState>();

            if (ActiveStateBase != null)
            {
                await ActiveStateBase.Exit();
            }

            ActiveStateBase = newState as TStateBase;
            await newState.Enter(payLoad);
        }

        private TStateBase ResolveState<TState>() where TState : StateBase
        {
            if (States == null || !States.TryGetValue(typeof(TState), out TStateBase state))
            {
                throw new InvalidOperationException(
                    $"State {typeof(TState).Name} is not registered in {GetType().Name}.");
            }

            return state;
        }

        public virtual Type GetCurrentState() =>
            ActiveStateBase?.GetType();

        /// <summary>
        /// Awaits the active state's <see cref="StateBase.Exit"/>, then clears <see cref="States"/>.
        /// Shares the same teardown task as <see cref="Dispose"/> when exit is deferred.
        /// </summary>
        public virtual async UniTask ShutdownAsync()
        {
            if (_disposed)
            {
                return;
            }

            if (_cleanupRunning)
            {
                await _exitThenClearStatesTask;
                return;
            }

            if (ActiveStateBase != null)
            {
                TStateBase exiting = ActiveStateBase;
                ActiveStateBase = null;
                _cleanupRunning = true;
                _exitThenClearStatesTask = ExitThenClearStatesAsync(exiting);
                await _exitThenClearStatesTask;
                return;
            }

            States?.Clear();
            _disposed = true;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (_cleanupRunning)
            {
                return;
            }

            if (ActiveStateBase != null)
            {
                TStateBase exiting = ActiveStateBase;
                ActiveStateBase = null;
                _cleanupRunning = true;
                _exitThenClearStatesTask = ExitThenClearStatesAsync(exiting);
                _exitThenClearStatesTask.Forget();
                return;
            }

            States?.Clear();
            _disposed = true;
        }

        private async UniTask ExitThenClearStatesAsync(TStateBase exiting)
        {
            try
            {
                await exiting.Exit();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                LoggingSystem.LogError($"State machine teardown: Exit failed. {ex}");
            }
            finally
            {
                States?.Clear();
                _cleanupRunning = false;
                _disposed = true;
            }
        }

        private void ThrowIfTeardownStarted()
        {
            if (_disposed || _cleanupRunning)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }
    }
}