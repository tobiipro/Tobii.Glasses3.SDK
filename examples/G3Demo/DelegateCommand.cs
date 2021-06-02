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

        public bool CanExecute(object parameter)
        {
            return !IsExecuting && (_canExecute == null || _canExecute());
        }

        public async void Execute(object parameter)
        {
            await _action();
        }

        public event EventHandler CanExecuteChanged;

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}