// MainViewModel.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

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
