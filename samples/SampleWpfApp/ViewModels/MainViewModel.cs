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

    // Raygun-handoff (2026-09-09) fixtures: long enough to outlive the payload's 5 s dispatcher wait,
    // so the action faults only after the caller has already been told ActionPending.
    private static readonly TimeSpan smSlowProbeDelay = TimeSpan.FromSeconds(6);

    private Customer? mSelectedCustomer;
    private string mSearchText = string.Empty;
    private string mProbeInput = string.Empty;
    private string mProbeStatus = string.Empty;
    private string mProbeResult = string.Empty;
    private int mUnobservedTaskExceptionCount;

    public MainViewModel()
    {
        Customers = [];
        SeedCustomers();
        SelectedCustomer = Customers[0];
        RunProbeCommand = new RelayCommand(RunProbe);
        BlockedCommand = new RelayCommand(RunProbe, static () => false);
        SlowFailingCommand = new RelayCommand(RunSlowFailingProbe);
        ForceGcCommand = new RelayCommand(ForceGc);
        LeakUnobservedFaultCommand = new RelayCommand(LeakUnobservedFault);

        // This app stands in for a customer host with a crash reporter: count every faulted Task the
        // finalizer finds unobserved, which is exactly how payload faults reached PASS's Raygun.
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
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

    /// <summary>A command whose <c>CanExecute</c> is always false (Raygun group 290708134232).</summary>
    public ICommand BlockedCommand { get; }

    /// <summary>Blocks the UI thread past the payload's dispatcher wait, then throws.</summary>
    public ICommand SlowFailingCommand { get; }

    /// <summary>Runs the collection plus finalizer pass that raises unobserved task faults.</summary>
    public ICommand ForceGcCommand { get; }

    /// <summary>Positive control: leaks one genuinely unobserved faulted Task.</summary>
    public ICommand LeakUnobservedFaultCommand { get; }

    /// <summary>How many unobserved task faults this process has recorded.</summary>
    public int UnobservedTaskExceptionCount => Volatile.Read(ref mUnobservedTaskExceptionCount);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void RunProbe()
    {
        ProbeStatus = ProbeStatusDone;
        ProbeResult = ProbeInput;
    }

    private static void RunSlowFailingProbe()
    {
        Thread.Sleep(smSlowProbeDelay);
        throw new InvalidOperationException("Slow probe failed after the dispatcher wait gave up.");
    }

    private static void ForceGc()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private static void LeakUnobservedFault()
    {
        _ = Task.FromException(new InvalidOperationException("Deliberately unobserved fault (positive control)."));
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Interlocked.Increment(ref mUnobservedTaskExceptionCount);
        e.SetObserved();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UnobservedTaskExceptionCount)));
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
