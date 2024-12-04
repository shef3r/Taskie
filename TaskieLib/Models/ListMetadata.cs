using System.Collections.Generic;
using System;
using System.ComponentModel;
using System.Drawing;
using Windows.UI.Xaml.Media;

public class ListMetadata : INotifyPropertyChanged
{
    private DateTime _creationDate;
    private string _name;
    private string _emoji;
    private int? _groupID;
    private string _titlefont;

    public string TitleFont
    {
        get { if (_titlefont != null) { return _titlefont; } else { return "Segoe UI Variable"; }; }
        set
        {
            _titlefont = value;
        }
    }

    public DateTime CreationDate
    {
        get { return _creationDate; }
        set
        {
            if (_creationDate != value)
            {
                _creationDate = value;
                OnPropertyChanged(nameof(CreationDate));
            }
        }
    }

    public string Name
    {
        get { return _name; }
        set
        {
            if (_name != value)
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }
    }

    public string Emoji
    {
        get { return _emoji; }
        set
        {
            if (_emoji != value)
            {
                _emoji = value;
                OnPropertyChanged(nameof(Emoji));
            }
        }
    }

    public int? GroupID
    {
        get { return _groupID; }
        set
        {
            if (value != _groupID)
            {
                _groupID = value;
                OnPropertyChanged(nameof(GroupID));
            }
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
