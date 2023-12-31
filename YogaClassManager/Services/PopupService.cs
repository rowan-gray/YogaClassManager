﻿namespace YogaClassManager.Services
{
    public class PopupService
    {
        public Task DisplayAlert(string title, string message, string cancel)
        {
            return Application.Current.MainPage.DisplayAlert(title, message, cancel);
        }

        public Task DisplayAlert(string title, string message, string cancel, FlowDirection flowDirection)
        {
            return Application.Current.MainPage.DisplayAlert(title, message, cancel, flowDirection);
        }

        public Task<bool> DisplayAlert(string title, string message, string accept, string cancel)
        {
            return Application.Current.MainPage.DisplayAlert(title, message, accept, cancel);
        }

        public Task<bool> DisplayAlert(string title, string message, string accept, string cancel, FlowDirection flowDirection)
        {
            return Application.Current.MainPage.DisplayAlert(title, message, accept, cancel, flowDirection);
        }

        public Task<string> DisplayActionSheet(string title, string cancel, string destruction, params string[] buttons)
        {
            return Application.Current.MainPage.DisplayActionSheet(title, cancel, destruction, buttons);
        }

        public Task<string> DisplayActionSheet(string title, string cancel, string destruction, FlowDirection flowDirection, params string[] buttons)
        {
            return Application.Current.MainPage.DisplayActionSheet(title, cancel, destruction, flowDirection, buttons);
        }

        public Task<string> DisplayPromptAsync(string title, string message, string accept = "OK", string cancel = "Cancel",
            string placeholder = null, int maxLength = -1, Keyboard keyboard = null, string initialValue = "")
        {
            return Application.Current.MainPage.DisplayPromptAsync(title, message, accept, cancel, placeholder, maxLength, keyboard, initialValue);
        }
    }
}
