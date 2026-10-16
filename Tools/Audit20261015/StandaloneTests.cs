using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

public static class StandaloneBattleTests
{
    public static string Run()
    {
        var failures = new List<string>();
        int passed=0, skipped=0;
        foreach (var type in typeof(StandaloneBattleTests).Assembly.GetTypes()
            .Where(t=>t.Namespace=="Grimhand.Battle.Tests" && t.IsClass && !t.IsAbstract))
        {
            foreach(var method in type.GetMethods())
            {
                var cases=method.GetCustomAttributes(typeof(TestCaseAttribute),true).Cast<TestCaseAttribute>().ToArray();
                if(cases.Length==0 && !method.IsDefined(typeof(TestAttribute),true)) continue;
                var arguments=cases.Length==0 ? new[]{Array.Empty<object>()} : cases.Select(c=>c.Arguments).ToArray();
                foreach(var args in arguments)
                {
                    var name=type.Name+"."+method.Name+"("+string.Join(",",args)+")";
                    try
                    {
                        var fixture=Activator.CreateInstance(type);
                        foreach(var setup in type.GetMethods().Where(m=>m.IsDefined(typeof(SetUpAttribute),true))) setup.Invoke(fixture,null);
                        method.Invoke(fixture,args);
                        foreach(var teardown in type.GetMethods().Where(m=>m.IsDefined(typeof(TearDownAttribute),true))) teardown.Invoke(fixture,null);
                        passed++;
                    }
                    catch(Exception e)
                    {
                        var cause=e is TargetInvocationException && e.InnerException!=null ? e.InnerException : e;
                        if(cause is IgnoreException) skipped++;
                        else failures.Add(name+": "+cause.Message);
                    }
                }
            }
        }
        return "Passed="+passed+" Failed="+failures.Count+" Skipped="+skipped+Environment.NewLine+string.Join(Environment.NewLine,failures);
    }
}
