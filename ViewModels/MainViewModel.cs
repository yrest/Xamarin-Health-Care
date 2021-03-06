﻿using Core;
using Prism.Commands;
using Prism.Events;
using SampleApplication.Commands;
using SampleApplication.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SampleApplication.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly IRepository _repository;
        private readonly IUpdateProviderLocationsCommand _updateLocationsCommand;

        private ObservableCollection<Appointment> _appointments;
        private HealthCareUser _currentUser;

        private bool _mainMenuOpen;
        private SubscriptionToken _modelUpdatedEventToken;
        private Appointment _selectedAppointment;

        public MainViewModel(IRepository repository, IUpdateProviderLocationsCommand updateLocationsCommand)
        {
            _repository = repository;
            _updateLocationsCommand = updateLocationsCommand;

            FetchAppointmentsCommand = new DelegateCommand(FetchAppointments);
            OpenSelectedAppointmentCommand = new DelegateCommand<Appointment>(OpenSelectedAppointment);
            CreateAppointmentNavigationCommand = new DelegateCommand(CreateAppointmentNavigate);
            MainMenuItemClickCommand = new DelegateCommand<MainMenuItem>(MainMenuItemClick);
            CancelAppointmentCommand = new DelegateCommand<Appointment>(CancelAppointment);
            Title = "Clinic 'O Care";

            MainMenuItems = new List<MainMenuItem>();
            MainMenuItems.Add(new MainMenuItem
            {
                Title = "Health Providers",
                IconSource = "circle_logo.png",
                ActionId = Constants.Navigation.ProviderListPage
            });
            MainMenuItems.Add(new MainMenuItem
            {
                Title = "Log out",
                IconSource = "logout_dark.png",
                ActionId = Constants.Navigation.Logout
            });

            //MainMenuItems.Add(new MainMenuItem
            //{
            //    Title = "About",
            //    IconSource = "greenlantern.png",
            //    ActionId = Constants.Navigation.AboutPage;
            //});
        }

        public ObservableCollection<Appointment> Appointments
        {
            get { return _appointments; }
            set { SetProperty(ref _appointments, value); }
        }

        public ICommand CancelAppointmentCommand { get; private set; }

        public ICommand CreateAppointmentNavigationCommand { get; private set; }

        public HealthCareUser CurrentUser
        {
            get { return _currentUser; }
            set { SetProperty(ref _currentUser, value); }
        }

        public ICommand FetchAppointmentsCommand { get; private set; }

        public ICommand MainMenuItemClickCommand { get; private set; }

        public IList<MainMenuItem> MainMenuItems { get; private set; }

        public bool MainMenuOpen
        {
            get { return _mainMenuOpen; }
            set { SetProperty(ref _mainMenuOpen, value); }
        }

        public ICommand OpenSelectedAppointmentCommand { get; private set; }

        public Appointment SelectedAppointment
        {
            get { return _selectedAppointment; }
            set { SetProperty(ref _selectedAppointment, value); }
        }

        public string Title { get; set; }

        public override void Closing()
        {
            CC.EventMessenger.GetEvent<ModelUpdatedMessageEvent<Appointment>>().Unsubscribe(_modelUpdatedEventToken);
        }

        public override async Task InitializeAsync(System.Collections.Generic.Dictionary<string, string> args)
        {
            _modelUpdatedEventToken = CC.EventMessenger.GetEvent<ModelUpdatedMessageEvent<Appointment>>().Subscribe(OnAppointmentUpdated);
            await FetchCurrentUserAsync();
            await FetchAppointmentsAsync();
            await UpdateProviderLocationsAsync(); //generate locations around the device current location
        }

        private async void CancelAppointment(Appointment appointment)
        {
            if (appointment != null)
            {
                await _repository.DeleteAppointmentAsync(appointment);
                Appointments.Remove(appointment);
            }
        }

        private async void CreateAppointmentNavigate()
        {
            await Navigation.NavigateAsync(Constants.Navigation.AppointmentPage);
        }

        private async void FetchAppointments()
        {
            await FetchAppointmentsAsync();
        }

        private async Task FetchAppointmentsAsync()
        {
            ShowBusy("fetching health records");

            try
            {
                FetchModelCollectionResult<Appointment> fetchResult = await _repository.FetchAppointmentsAsync(CurrentUser.Id, null);

                if (fetchResult.IsValid())
                {
                    Appointments = fetchResult.ModelCollection.AsObservableCollection();
                    await Task.Delay(1000); //simulate fetching online data
                }
                else
                {
                    NotBusy();
                    await CC.UserNotifier.ShowMessageAsync(fetchResult.Notification.ToString(), "Fetch Appointments Failed :(");
                }
            }
            finally
            {
                NotBusy();
            }
        }

        private async Task FetchCurrentUserAsync()
        {
            var fetchResult = await _repository.GetCurrentUserAsync();

            CurrentUser = fetchResult.Model;
        }

        private async Task LogoutAsync()
        {
            if (CurrentUser != null)
            {
                CurrentUser.IsLoggedIn = false;
                await _repository.SaveCurrentUserAsync(CurrentUser, ModelUpdateEvent.Updated);
            }
            await CC.Navigation.NavigateAsync(Constants.Navigation.AuthPage, null, false, false, true);
        }

        private async void MainMenuItemClick(MainMenuItem menuItem)
        {
            MainMenuOpen = false;

            if (menuItem != null)
            {
                switch (menuItem.ActionId)
                {
                    case Constants.Navigation.Logout:
                        await LogoutAsync();
                        break;

                    case Constants.Navigation.ProviderListPage:
                        await NavigateToProviderListAsync();

                        break;
                }
            }
        }

        private async Task NavigateToProviderListAsync()
        {
            await CC.Navigation.NavigateAsync(Constants.Navigation.ProviderListPage);
        }

        private void OnAppointmentUpdated(ModelUpdatedMessageResult<Appointment> updateResult)
        {
            Appointments.UpdateCollection(updateResult.UpdatedModel, updateResult.UpdateEvent);
        }

        private async void OpenSelectedAppointment(Appointment appointment)
        {
            SelectedAppointment = appointment;
            await OpenSelectedAppointmentAsync();
        }

        private async Task OpenSelectedAppointmentAsync()
        {
            if (SelectedAppointment != null)
            {
                Dictionary<string, string> args = new Dictionary<string, string>
                {
                    {Constants.Parameters.Id, SelectedAppointment.Id}
                };

                await Navigation.NavigateAsync(Constants.Navigation.AppointmentPage, args);
            }
        }

        private async Task<Notification> UpdateProviderLocationsAsync()
        {
            var retResult = Notification.Success();
            ShowBusy("fetching health records");

            try
            {
                var updateResult = await _updateLocationsCommand.ExecuteAsync(null);
            }
            finally
            {
                NotBusy();
            }

            return retResult;
        }
    }
}