﻿using UnityEngine;
using System.Collections.Generic;
using System.Diagnostics;
using System;
using System.Reflection;
using System.Text.RegularExpressions;

public interface ILogger
{
    void Log(LogInformation log);
}

public interface IFilter
{

}

[AttributeUsage(AttributeTargets.Method)]
public class ExcludeStackTrace : Attribute { }

[AttributeUsage(AttributeTargets.Method)]
public class OnlyUnityLog : Attribute { }
public static class XLogger
{
    public static int MaxMessage = 500;
    public static bool UseBothSystem = true;
    public static string UnityNewLine = "/n";
    public static char DirectorySeparator = '/';

    static List<ILogger> LoggerList = new List<ILogger>();
    static List<IFilter> FilterList = new List<IFilter>();
    static LinkedList<LogInformation> RecentMessages = new LinkedList<LogInformation>();
    static long StartTimeTicks;
    static bool Logged;
    static Regex MessageRegex;

    static XLogger()
    {
        Application.logMessageReceivedThreaded += UnityLogHandler;
        StartTimeTicks = DateTime.Now.Ticks;
        MessageRegex = new Regex(@"(.*)\((\d+).*\)");
    }

    [ExcludeStackTrace]
    static void UnityLogHandler(string unityLogMessage, string unityStackFrame, LogType logType)
    {
        lock (LoggerList)
        {
            if (!Logged)
            {
                try
                {
                    Logged = true;
                    LogLevel logLevel;
                    switch (logType)
                    {
                        case LogType.Warning:
                            logLevel = LogLevel.Warning;
                            break;
                        case LogType.Assert:
                            logLevel = LogLevel.Error;
                            break;
                        case LogType.Error:
                            logLevel = LogLevel.Error;
                            break;
                        case LogType.Exception:
                            logLevel = LogLevel.Error;
                            break;
                        default:
                            logLevel = LogLevel.Message;
                            break;
                    }
                    LogStackFrame orginStackFrame;
                    List<LogStackFrame> stackFrames = new List<LogStackFrame>();
                    bool OnlyUnityLog = GetStackFramListFromUnity(ref stackFrames, out orginStackFrame);
                    if (OnlyUnityLog)
                        return;
                    if(stackFrames.Count ==0)
                        stackFrames = GetStackFrameFromeUnity(unityStackFrame, out orginStackFrame);
                    string fileName = "";
                    int lineNumber = 0;
                    if(ExtractInformationFromUnityLog(unityLogMessage,ref fileName,ref lineNumber))
                    {
                        stackFrames.Insert(0, new LogStackFrame(unityLogMessage, fileName, lineNumber));
                    }
                    var logInformation = new LogInformation(null, "", logLevel, stackFrames,
                        orginStackFrame, message: unityLogMessage);
                    RecentMessages.AddLast(logInformation);
                    while (RecentMessages.Count > MaxMessage)
                    {
                        RecentMessages.RemoveFirst();
                    }
                    /*foreach(var logs in LoggerList)
                    {
                        if (logs == null)
                            LoggerList.Remove(logs);
                    }*/
                    ///TODO
                    ///
                    LoggerList.RemoveAll(l => l == null);
                    LoggerList.ForEach(l => l.Log(logInformation));
                }
                finally
                {
                    Logged = false;
                }
            }
        }
    }

    [ExcludeStackTrace]
    static public void Log(UnityEngine.Object origin, LogLevel logLevel,
        string Channel,System.Object message,params object[] paramsObject)
    {
        lock (LoggerList)
        {

        }
    }

    static List<LogStackFrame> GetStackFrameFromeUnity(string unityStackFrame,out LogStackFrame orginStackFrame)
    {
        var newLines = Regex.Split(unityStackFrame, UnityNewLine);
        List<LogStackFrame> stackFrames = new List<LogStackFrame>();
        foreach(var line in newLines)
        {
            var frame = new LogStackFrame(line);
            if (!string.IsNullOrEmpty(frame.FormatMethodNameByFile))
            {
                //change!
                stackFrames.Add(frame);
            }
        }
        if (stackFrames.Count > 0)
            orginStackFrame = stackFrames[0];
        else
            orginStackFrame = null;
        return stackFrames;
    }

    static bool GetStackFramListFromUnity(ref List<LogStackFrame> stackFrameList,out LogStackFrame orginStackFrame)
    {
        stackFrameList.Clear();
        orginStackFrame = null;
        StackTrace stackTrace = new StackTrace(true);
        StackFrame[] stackFrames = stackTrace.GetFrames();
        bool MeetFirstIgnoredMethod = false;
        for (int i = stackFrames.Length - 1; i > 0; i--)
        {
            StackFrame tempStackFrame = stackFrames[i];
            var method = tempStackFrame.GetMethod();
            if (method.IsDefined(typeof(OnlyUnityLog), true))
                return true;
            if (!method.IsDefined(typeof(ExcludeStackTrace), true))
            {
                UnityMethod.MethodMode mode = UnityMethod.GetMehodMode(method);
                bool isShowed;
                if (mode == UnityMethod.MethodMode.Show)
                    isShowed = true;
                else
                    isShowed = false;
                if(mode == UnityMethod.MethodMode.ShowFirst)
                {
                    if (!MeetFirstIgnoredMethod)
                    {
                        MeetFirstIgnoredMethod = true;
                        mode = UnityMethod.MethodMode.Show;
                    }
                    else
                        mode = UnityMethod.MethodMode.Hide;
                }
                if(mode == UnityMethod.MethodMode.Show)
                {
                    LogStackFrame logStackFrame = new LogStackFrame(tempStackFrame);
                    stackFrameList.Add(logStackFrame);
                    if (isShowed)
                        orginStackFrame = logStackFrame;
                }

            }
        }
        return false;
    }

    static public bool ExtractInformationFromUnityLog(string log, ref string fileName, ref int lineNumber)
    {
        var match = MessageRegex.Matches(log);
        if (match.Count > 0)
        {
            fileName = match[0].Groups[1].Value;
            lineNumber = Convert.ToInt32(match[0].Groups[2].Value);
            return true;
        }
        return false;
    }


}
