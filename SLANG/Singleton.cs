using System;

public class Singleton<T> where T : class, new()
{
    protected Singleton()
    {
        System.Diagnostics.Debug.Assert( null == _instance );
    }
    private static readonly T _instance = new T();
    
    public static T Instance {
         get {
             return _instance;
         }
    }
}
