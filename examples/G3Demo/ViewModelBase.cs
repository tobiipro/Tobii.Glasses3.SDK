using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using G3Demo.Annotations;

namespace G3Demo
{
    public class ViewModelBase : INotifyPropertyChanged
    {
        public Dispatcher Dispatcher { get; }

        public ViewModelBase(Dispatcher dispatcher)
        {
            Dispatcher = dispatcher;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected void RaiseCanExecuteChange(DelegateCommand command)
        {
            Dispatcher.Invoke(command.RaiseCanExecuteChanged);
        }
    }
}