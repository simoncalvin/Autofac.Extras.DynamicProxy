﻿using Autofac.Extras.DynamicProxy2;
using Castle.DynamicProxy;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Autofac.Extras.Tests.DynamicProxy2
{
    [TestFixture]
    public class AttributedInterfaceInterceptionWithOptionsFixture
    {
        [Intercept(typeof(AddOneInterceptor))]
        [Intercept(typeof(AddTenInterceptor))]
        public interface IHasIAndJ
        {
            int GetI();
            int GetJ();
        }

        public class C : IHasIAndJ
        {
            public int I { get; private set; }
            public int J { get; private set; }

            public C()
            {
                I = J = 10;
            }

            public int GetI()
            {
                return I;
            }

            public int GetJ()
            {
                return J;
            }
        }

        class InterceptOnlyJ : IProxyGenerationHook
        {

            public void MethodsInspected()
            {
            }

            public void NonProxyableMemberNotification(Type type, MemberInfo memberInfo)
            {
            }

            public bool ShouldInterceptMethod(Type type, MethodInfo methodInfo)
            {
                return methodInfo.Name.Equals("GetJ");
            }

        }

        class MyInterceptorSelector : IInterceptorSelector
        {
            public IInterceptor[] SelectInterceptors(Type type, MethodInfo method, IInterceptor[] interceptors)
            {
                return method.Name == "GetI"
                    ? interceptors.OfType<AddOneInterceptor>().ToArray<IInterceptor>()
                    : interceptors.OfType<AddTenInterceptor>().ToArray<IInterceptor>();
            }
        }

        class AddOneInterceptor : IInterceptor
        {
            public void Intercept(IInvocation invocation)
            {
                invocation.Proceed();
                if (invocation.Method.Name == "GetI" || invocation.Method.Name == "GetJ")
                    invocation.ReturnValue = 1 + (int)invocation.ReturnValue;
            }
        }

        class AddTenInterceptor : IInterceptor
        {
            public void Intercept(IInvocation invocation)
            {
                invocation.Proceed();
                if (invocation.Method.Name == "GetJ")
                    invocation.ReturnValue = 10 + (int)invocation.ReturnValue;
            }
        }

        [Test]
        public void CanCreateMixinWithAttributeInterceptors()
        {
            var options = new ProxyGenerationOptions();
            options.AddMixinInstance(new Dictionary<int, int>());

            var builder = new ContainerBuilder();
            builder.RegisterType<C>().As<IHasIAndJ>().EnableInterfaceInterceptors(options);
            builder.RegisterType<AddOneInterceptor>();
            builder.RegisterType<AddTenInterceptor>();
            var cpt = builder.Build().Resolve<IHasIAndJ>();

            var dict = cpt as IDictionary<int, int>;

            Assert.IsNotNull(dict);

            dict.Add(1, 2);

            Assert.AreEqual(2, dict[1]);

            dict.Clear();

            Assert.IsEmpty(dict);
        }

        [Test]
        public void CanInterceptOnlySpecificMethods()
        {
            var options = new ProxyGenerationOptions(new InterceptOnlyJ());

            var builder = new ContainerBuilder();
            builder.RegisterType<C>().As<IHasIAndJ>().EnableInterfaceInterceptors(options);
            builder.RegisterType<AddOneInterceptor>();
            builder.RegisterType<AddTenInterceptor>();
            var cpt = builder.Build().Resolve<IHasIAndJ>();

            Assert.AreEqual(10, cpt.GetI());
            Assert.AreEqual(21, cpt.GetJ());
        }

        [Test]
        public void CanResolveInterceptorFromComponentContext()
        {
            const string optionsServiceName = "improbable service name";

            var builder = new ContainerBuilder();
            builder.Register(c => new ProxyGenerationOptions(new InterceptOnlyJ())).Named<ProxyGenerationOptions>(optionsServiceName);
            builder.RegisterType<C>().As<IHasIAndJ>().EnableInterfaceInterceptors(optionsServiceName);
            builder.RegisterType<AddOneInterceptor>();
            builder.RegisterType<AddTenInterceptor>();
            var cpt = builder.Build().Resolve<IHasIAndJ>();

            Assert.AreEqual(10, cpt.GetI());
            Assert.AreEqual(21, cpt.GetJ());
        }

        [Test]
        public void CanInterceptMethodsWithSpecificInterceptors()
        {
            var options = new ProxyGenerationOptions { Selector = new MyInterceptorSelector() };

            var builder = new ContainerBuilder();
            builder.RegisterType<C>().As<IHasIAndJ>().EnableInterfaceInterceptors(options);
            builder.RegisterType<AddOneInterceptor>();
            builder.RegisterType<AddTenInterceptor>();
            var cpt = builder.Build().Resolve<IHasIAndJ>();

            Assert.AreEqual(11, cpt.GetI());
            Assert.AreEqual(20, cpt.GetJ());
        }
    }
}
