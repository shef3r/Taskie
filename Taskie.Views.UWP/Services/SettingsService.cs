using System;
using Taskie.Services;

namespace Taskie.Views.UWP.Services
{
    public class SettingsService : ISettingsService
    {
        private SettingsService()
        {
        }

        public static SettingsService Instance { get; } = new();

        public T Get<T>(string key)
        {
            // TODO: Implement
            return default;
        }

        public void Set<T>(string key, T value)
        {
            // TODO: Implement
        }

        public event EventHandler<string> Changed;
    }
}