using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace G3Demo
{
    public class DelegateCommand : ICommand
    {
        private readonly Func<Task> _action;
        private readonly Func<bool> _canExecute;
        private bool _isExecuting;
        private readonly Action<object> _paramAction;

        public bool IsExecuting
        {
            get => _isExecuting;
            private set
            {
                _isExecuting = value;
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public DelegateCommand(Func<Task> action, Func<bool> canExecute)
        {
            IsExecuting = true;
            _action = action;
            _canExecute = canExecute;
            IsExecuting = false;
        }
        
        public DelegateCommand(Action<object> action, Func<bool> canExecute)
        {
            IsExecuting = true;
             _paramAction = action;
            _canExecute = canExecute;
            IsExecuting = false;
        }

        public bool CanExecute(object parameter)
        {
            return !IsExecuting && (_canExecute == null || _canExecute());
        }

        public async void Execute(object parameter)
        {
            IsExecuting = true;
            if (_paramAction != null)
                _paramAction(parameter);
            else
            {
                await _action();
            }
            IsExecuting = false;
        }

        public event EventHandler CanExecuteChanged;

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}