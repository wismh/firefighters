using Cysharp.Threading.Tasks;

namespace MyToolkit.StateMachine {
    public abstract class PayLoadedStateBase<TPayload> : StateBase 
    {
        public virtual UniTask Enter(TPayload payLoad)
        {
            return Enter();
        }
    }
}