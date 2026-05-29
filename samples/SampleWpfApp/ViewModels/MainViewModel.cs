namespace SampleWpfApp.ViewModels;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private Customer? mSelectedCustomer;
    private string mSearchText = string.Empty;

    public MainViewModel()
    {
        Customers = new ObservableCollection<Customer>();
        SeedCustomers();
        SelectedCustomer = Customers[0];
    }

    public ObservableCollection<Customer> Customers { get; }

    public Customer? SelectedCustomer
    {
        get => mSelectedCustomer;
        set => SetField(ref mSelectedCustomer, value);
    }

    public string SearchText
    {
        get => mSearchText;
        set => SetField(ref mSearchText, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SeedCustomers()
    {
        const int seedCount = 1000;
        for (int i = 0; i < seedCount; i++)
        {
            Customers.Add(new Customer
            {
                Name = $"Customer {i:D4}",
                Email = $"customer{i:D4}@example.com",
                Address = new Address
                {
                    Street = $"{i + 1} Main St",
                    City = "Springfield",
                    PostalCode = $"{10000 + i}"
                }
            });
        }
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        bool changed = !EqualityComparer<T>.Default.Equals(field, value);
        if (changed)
        {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
