// MainViewModel.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SampleWpfApp.ViewModels;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private const string ProbeStatusDone = "done";

    private Customer? mSelectedCustomer;
    private string mSearchText = string.Empty;
    private string mProbeInput = string.Empty;
    private string mProbeStatus = string.Empty;
    private string mProbeResult = string.Empty;

    public MainViewModel()
    {
        Customers = [];
        SeedCustomers();
        SelectedCustomer = Customers[0];
        RunProbeCommand = new RelayCommand(RunProbe);
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

    public string ProbeInput
    {
        get => mProbeInput;
        set => SetField(ref mProbeInput, value);
    }

    public string ProbeStatus
    {
        get => mProbeStatus;
        set => SetField(ref mProbeStatus, value);
    }

    public string ProbeResult
    {
        get => mProbeResult;
        set => SetField(ref mProbeResult, value);
    }

    public ICommand RunProbeCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void RunProbe()
    {
        ProbeStatus = ProbeStatusDone;
        ProbeResult = ProbeInput;
    }

    private void SeedCustomers()
    {
        const int SeedCount = 1000;
        for (int i = 0; i < SeedCount; i++)
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
