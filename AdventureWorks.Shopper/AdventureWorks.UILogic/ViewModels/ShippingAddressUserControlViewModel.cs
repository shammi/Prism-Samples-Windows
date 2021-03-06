using AdventureWorks.UILogic.Models;
using AdventureWorks.UILogic.Repositories;
using AdventureWorks.UILogic.Services;
using Prism.Windows.AppModel;
using Prism.Windows.Mvvm;
using Prism.Windows.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.Xaml.Navigation;

namespace AdventureWorks.UILogic.ViewModels
{
    public class ShippingAddressUserControlViewModel : ViewModelBase, IShippingAddressUserControlViewModel
    {
        private readonly ICheckoutDataRepository _checkoutDataRepository;
        private readonly ILocationService _locationService;
        private readonly IResourceLoader _resourceLoader;
        private readonly IAlertMessageService _alertMessageService;
        private string _addressId;
        private Address _address;
        private bool _loadDefault;
        private IReadOnlyCollection<ComboBoxItemValue> _states;

        public ShippingAddressUserControlViewModel(ICheckoutDataRepository checkoutDataRepository, ILocationService locationService, IResourceLoader resourceLoader, IAlertMessageService alertMessageService)
        {
            _address = new Address();
            _checkoutDataRepository = checkoutDataRepository;
            _locationService = locationService;
            _resourceLoader = resourceLoader;
            _alertMessageService = alertMessageService;
        }

        [RestorableState]
        public Address Address
        {
            get { return _address; }
            set { SetProperty(ref _address, value); }
        }

        public IReadOnlyCollection<ComboBoxItemValue> States
        {
            get { return _states; }
            private set { SetProperty(ref _states, value); }
        }

        public override async void OnNavigatedTo(NavigatedToEventArgs e, Dictionary<string, object> viewState)
        {
            // The States collection needs to be populated before setting the State property
            await PopulateStatesAsync();

            if (viewState != null)
            {
                base.OnNavigatedTo(e, viewState);

                if (e.NavigationMode == NavigationMode.Refresh)
                {
                    // Restore the errors collection manually
                    var errorsCollection = RetrieveEntityStateValue<IDictionary<string, ReadOnlyCollection<string>>>("errorsCollection", viewState);

                    if (errorsCollection != null)
                    {
                        _address.SetAllErrors(errorsCollection);
                    }
                }
            }

            if (e.NavigationMode == NavigationMode.New)
            {
                _addressId = e.Parameter as string;
                if (_addressId != null)
                {
                    Address = await _checkoutDataRepository.GetShippingAddressAsync(_addressId);
                    return;
                }

                if (_loadDefault)
                {
                    var defaultAddress = await _checkoutDataRepository.GetDefaultShippingAddressAsync();
                    if (defaultAddress != null)
                    {
                        // Update the information and validate the values
                        Address.FirstName = defaultAddress.FirstName;
                        Address.MiddleInitial = defaultAddress.MiddleInitial;
                        Address.LastName = defaultAddress.LastName;
                        Address.StreetAddress = defaultAddress.StreetAddress;
                        Address.OptionalAddress = defaultAddress.OptionalAddress;
                        Address.City = defaultAddress.City;
                        Address.State = defaultAddress.State;
                        Address.ZipCode = defaultAddress.ZipCode;
                        Address.Phone = defaultAddress.Phone;
                    }
                }
            }
        }

        public override void OnNavigatingFrom(NavigatingFromEventArgs e, Dictionary<string, object> viewState, bool suspending)
        {
            base.OnNavigatingFrom(e, viewState, suspending);

            // Store the errors collection manually
            if (viewState != null)
            {
                AddEntityStateValue("errorsCollection", _address.GetAllErrors(), viewState);
            }
        }

        public async Task PopulateStatesAsync()
        {
            string errorMessage = string.Empty;
            try
            {
                var states = await _locationService.GetStatesAsync();

                var items = new List<ComboBoxItemValue> { new ComboBoxItemValue() { Id = string.Empty, Value = _resourceLoader.GetString("State") } };
                items.AddRange(states.Select(state => new ComboBoxItemValue() { Id = state, Value = state }));
                States = new ReadOnlyCollection<ComboBoxItemValue>(items);

                // Select the first item on the list
                // But disable validation first, because we don't want to fire validation at this point
                _address.IsValidationEnabled = false;
                _address.State = States.First().Id;
                _address.IsValidationEnabled = true;
            }
            catch (Exception ex)
            {
                errorMessage = string.Format(CultureInfo.CurrentCulture,
                                             _resourceLoader.GetString("GeneralServiceErrorMessage"),
                                             Environment.NewLine,
                                             ex.Message);
            }

            if (!string.IsNullOrWhiteSpace(errorMessage))
            {
                await _alertMessageService.ShowAsync(errorMessage, _resourceLoader.GetString("ErrorServiceUnreachable"));
            }
        }

        public void SetLoadDefault(bool loadDefault)
        {
            _loadDefault = loadDefault;
        }

        public bool ValidateForm()
        {
            return _address.ValidateProperties();
        }

        public async Task ProcessFormAsync()
        {
            if (_addressId == null)
            {
                // Add Address but check for duplicate
                var existingAddresses = await _checkoutDataRepository.GetAllShippingAddressesAsync();
                var matchingExistingAddress = Address.FindMatchingAddress(Address, existingAddresses);
                if (matchingExistingAddress != null)
                {
                    Address = matchingExistingAddress;
                }
                else
                {
                    await _checkoutDataRepository.SaveShippingAddressAsync(Address);
                }
            }
            else
            {
                // Updated existing address
                await _checkoutDataRepository.SaveShippingAddressAsync(Address);
            }
        }
    }
}
