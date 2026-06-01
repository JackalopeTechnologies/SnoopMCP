// Customer.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SampleWpfApp.ViewModels;

using System.ComponentModel;
using System.Runtime.CompilerServices;

public sealed class Customer : INotifyPropertyChanged
{
    private string mName = string.Empty;
    private string mEmail = string.Empty;
    private Address? mAddress;

    public string Name
    {
        get => mName;
        set => SetField(ref mName, value);
    }

    public string Email
    {
        get => mEmail;
        set => SetField(ref mEmail, value);
    }

    public Address? Address
    {
        get => mAddress;
        set => SetField(ref mAddress, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

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
