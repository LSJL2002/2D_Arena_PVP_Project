using Fusion;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

public struct NavigationData
{
    public string ScreenName;
    public object Data;

    public NavigationData(string screenName, object data = null)
    {
        ScreenName = screenName;
        Data = data;
    }
}
public class LobbyScreenModel
{
    private readonly Stack<NavigationData> navigationStack = new();

    public NavigationData CurrentScreen => navigationStack.Count > 0 ? navigationStack.Peek() : default;

    public bool canGoBack => navigationStack.Count > 1;

    public void NavigateTo(string screenName, object data = null)
    {
        navigationStack.Push(new NavigationData(screenName, data));
    }

    public void NavigateBack()
    {
        if (canGoBack)
        {
            navigationStack.Pop();
        }
    }
}
