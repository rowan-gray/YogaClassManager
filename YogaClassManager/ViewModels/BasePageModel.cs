using CommunityToolkit.Mvvm.ComponentModel;

namespace YogaClassManager.ViewModels
{
    public partial class BasePageModel : ObservableObject
    {
        private int busyDepth = 0;
        private bool isBusy;
        public bool IsBusy
        {
            get => isBusy;
            private set
            {
                OnPropertyChanging(nameof(IsNotBusy));
                SetProperty(ref isBusy, value);
                OnPropertyChanged(nameof(IsNotBusy));
            }
        }
        public bool IsNotBusy => !IsBusy;

        public void StartBusy()
        {
            if (busyDepth == 0)
                IsBusy = true;
            busyDepth++;
        }

        public void EndBusy()
        {
            busyDepth--;
            if (busyDepth == 0)
                IsBusy = false;
        }
    }
}
