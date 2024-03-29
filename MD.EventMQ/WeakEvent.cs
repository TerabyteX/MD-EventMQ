﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace MD.EventMQ
{
    public sealed class WeakEvent<T> where T : class
    {
        struct EventEntry
        {
            public readonly WeakEventForwarderProvider.ForwarderDelegate Forwarder;
            public readonly MethodInfo TargetMethod;
            public readonly WeakReference TargetReference;

            public EventEntry(WeakEventForwarderProvider.ForwarderDelegate forwarder, MethodInfo targetMethod, WeakReference targetReference)
            {
                this.Forwarder = forwarder;
                this.TargetMethod = targetMethod;
                this.TargetReference = targetReference;
            }
        }

        readonly List<EventEntry> eventEntries = new List<EventEntry>();

        static WeakEvent()
        {
            if (!typeof(T).IsSubclassOf(typeof(Delegate)))
                throw new ArgumentException("T must be a delegate type");

            MethodInfo invoke = typeof(T).GetMethod("Invoke");
            if (invoke == null || invoke.GetParameters().Length != 2)
                throw new ArgumentException("T must be a delegate type taking 2 parameters");

            ParameterInfo senderParameter = invoke.GetParameters()[0];
            if (senderParameter.ParameterType != typeof(object))
                throw new ArgumentException("The first delegate parameter must be of type 'object'");

            ParameterInfo argsParameter = invoke.GetParameters()[1];
            if (!(typeof(EventArgs).IsAssignableFrom(argsParameter.ParameterType)))
                throw new ArgumentException("The second delegate parameter must be derived from type 'EventArgs'");

            if (invoke.ReturnType != typeof(void))
                throw new ArgumentException("The delegate return type must be void.");
        }

        public void Add(T eh)
        {
            if (eh != null)
            {
                Delegate d = (Delegate)(object)eh;
                if (eventEntries.Count == eventEntries.Capacity)
                    RemoveDeadEntries();
                MethodInfo targetMethod = d.Method;
                object targetInstance = d.Target;
                WeakReference target = targetInstance != null ? new WeakReference(targetInstance) : null;
                eventEntries.Add(new EventEntry(WeakEventForwarderProvider.GetForwarder(targetMethod), targetMethod, target));
            }
        }

        void RemoveDeadEntries()
        {
            eventEntries.RemoveAll(ee => ee.TargetReference != null && !ee.TargetReference.IsAlive);
        }

        public void Remove(T eh)
        {
            if (eh != null)
            {
                Delegate d = (Delegate)(object)eh;
                object targetInstance = d.Target;
                MethodInfo targetMethod = d.Method;
                for (int i = eventEntries.Count - 1; i >= 0; i--)
                {
                    EventEntry entry = eventEntries[i];
                    if (entry.TargetReference != null)
                    {
                        object target = entry.TargetReference.Target;
                        if (target == null)
                        {
                            eventEntries.RemoveAt(i);
                        }
                        else if (target == targetInstance && entry.TargetMethod == targetMethod)
                        {
                            eventEntries.RemoveAt(i);
                            break;
                        }
                    }
                    else
                    {
                        if (targetInstance == null && entry.TargetMethod == targetMethod)
                        {
                            eventEntries.RemoveAt(i);
                            break;
                        }
                    }
                }
            }
        }

        public void Raise(object sender, EventArgs e)
        {
            bool needsCleanup = false;
            foreach (EventEntry ee in eventEntries.ToArray())
            {
                needsCleanup |= ee.Forwarder(ee.TargetReference, sender, e);
            }
            if (needsCleanup)
                RemoveDeadEntries();
        }
    }

    static class WeakEventForwarderProvider
    {
        static readonly MethodInfo getTarget = typeof(WeakReference).GetMethod("get_Target");
        static readonly Type[] forwarderParameters = { typeof(WeakReference), typeof(object), typeof(EventArgs) };
        internal delegate bool ForwarderDelegate(WeakReference wr, object sender, EventArgs e);

        static readonly Dictionary<MethodInfo, ForwarderDelegate> forwarders = new Dictionary<MethodInfo, ForwarderDelegate>();

        internal static ForwarderDelegate GetForwarder(MethodInfo method)
        {
            lock (forwarders)
            {
                ForwarderDelegate d;
                if (forwarders.TryGetValue(method, out d))
                    return d;
            }

            if (method.DeclaringType.GetCustomAttributes(typeof(CompilerGeneratedAttribute), false).Length != 0)
                throw new ArgumentException("Cannot create weak event to anonymous method with closure.");
            var parameters = method.GetParameters();

            Debug.Assert(getTarget != null);

            DynamicMethod dm = new DynamicMethod("WeakEvent", typeof(bool), forwarderParameters, method.DeclaringType);

            ILGenerator il = dm.GetILGenerator();

            if (!method.IsStatic)
            {
                il.Emit(OpCodes.Ldarg_0);
                il.EmitCall(OpCodes.Callvirt, getTarget, null);
                il.Emit(OpCodes.Dup);
                Label label = il.DefineLabel();
                il.Emit(OpCodes.Brtrue, label);
                il.Emit(OpCodes.Pop);
                il.Emit(OpCodes.Ldc_I4_1);
                il.Emit(OpCodes.Ret);
                il.MarkLabel(label);
            }
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Castclass, parameters[1].ParameterType);
            il.EmitCall(OpCodes.Call, method, null);
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Ret);

            ForwarderDelegate fd = (ForwarderDelegate)dm.CreateDelegate(typeof(ForwarderDelegate));
            lock (forwarders)
            {
                forwarders[method] = fd;
            }
            return fd;
        }
    }
}
