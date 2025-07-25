﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeStyle;
using Microsoft.CodeAnalysis.CSharp.CodeStyle;
using Microsoft.CodeAnalysis.CSharp.GenerateConstructor;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.Diagnostics;
using Microsoft.CodeAnalysis.Editor.UnitTests.Diagnostics.NamingStyles;
using Microsoft.CodeAnalysis.Test.Utilities;
using Roslyn.Test.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.GenerateConstructor;

[Trait(Traits.Feature, Traits.Features.CodeActionsGenerateConstructor)]
public sealed class GenerateConstructorTests(ITestOutputHelper logger)
    : AbstractCSharpDiagnosticProviderBasedUserDiagnosticTest_NoEditor(logger)
{
    internal override (DiagnosticAnalyzer?, CodeFixProvider) CreateDiagnosticProviderAndFixer(Workspace workspace)
        => (null, new GenerateConstructorCodeFixProvider());

    private readonly NamingStylesTestOptionSets options = new NamingStylesTestOptionSets(LanguageNames.CSharp);

    [Fact]
    public Task TestWithSimpleArgument()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                void M()
                {
                    new [|C|](1);
                }
            }
            """,
            """
            class C
            {
                private int v;

                public C(int v)
                {
                    this.v = v;
                }

                void M()
                {
                    new C(1);
                }
            }
            """);

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/44537")]
    public Task TestWithSimpleArgument_WithProperties()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                void M()
                {
                    new [|C|](1);
                }
            }
            """,
            """
            class C
            {
                public C(int v)
                {
                    V = v;
                }

                public int V { get; }

                void M()
                {
                    new C(1);
                }
            }
            """, index: 1);

    [Fact]
    public Task TestWithSimpleArgument_NoMembers()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                void M()
                {
                    new [|C|](1);
                }
            }
            """,
            """
            class C
            {
                public C(int v)
                {
                }

                void M()
                {
                    new C(1);
                }
            }
            """, index: 2);

    [Fact]
    public Task TestWithSimpleArgument_UseExpressionBody1()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                void M()
                {
                    new [|C|](1);
                }
            }
            """,
            """
            class C
            {
                private int v;

                public C(int v) => this.v = v;

                void M()
                {
                    new C(1);
                }
            }
            """,
            new(options: Option(CSharpCodeStyleOptions.PreferExpressionBodiedConstructors, CSharpCodeStyleOptions.WhenPossibleWithSilentEnforcement)));

    [Fact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/910589")]
    public async Task TestWithNoArgs()
    {
        var input =
            """
            class C
            {
                public C(int v)
                {
                }

                void M()
                {
                    new [|C|]();
                }
            }
            """;

        await TestActionCountAsync(input, 1);
        await TestInRegularAndScriptAsync(
input,
"""
class C
{
    public C()
    {
    }

    public C(int v)
    {
    }

    void M()
    {
        new C();
    }
}
""");
    }

    [Fact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/910589")]
    public Task TestWithNamedArg()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                void M()
                {
                    new [|C(goo: 1)|];
                }
            }
            """,
            """
            class C
            {
                private int goo;

                public C(int goo)
                {
                    this.goo = goo;
                }

                void M()
                {
                    new C(goo: 1);
                }
            }
            """);

    [Fact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/910589")]
    public async Task TestWithExistingField1()
    {
        const string input =
            """
            class C
            {
                void M()
                {
                    new [|D(goo: 1)|];
                }
            }

            class D
            {
                private int goo;
            }
            """;
        await TestActionCountAsync(input, 1);
        await TestInRegularAndScriptAsync(
     input,
     """
     class C
     {
         void M()
         {
             new D(goo: 1);
         }
     }

     class D
     {
         private int goo;

         public D(int goo)
         {
             this.goo = goo;
         }
     }
     """);
    }

    [Fact]
    public Task TestWithExistingField2()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                void M()
                {
                    new [|D|](1);
                }
            }

            class D
            {
                private string v;
            }
            """,
            """
            class C
            {
                void M()
                {
                    new D(1);
                }
            }

            class D
            {
                private string v;
                private int v1;

                public D(int v1)
                {
                    this.v1 = v1;
                }
            }
            """);

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/44537")]
    public Task TestWithExistingField2_WithProperties()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                void M()
                {
                    new [|D|](1);
                }
            }

            class D
            {
                private string v;
            }
            """,
            """
            class C
            {
                void M()
                {
                    new D(1);
                }
            }

            class D
            {
                private string v;

                public D(int v1)
                {
                    V = v1;
                }

                public int V { get; }
            }
            """, index: 1);

    [Fact]
    public Task TestWithExistingField2_NoMembers()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                void M()
                {
                    new [|D|](1);
                }
            }

            class D
            {
                private string v;
            }
            """,
            """
            class C
            {
                void M()
                {
                    new D(1);
                }
            }

            class D
            {
                private string v;

                public D(int v1)
                {
                }
            }
            """, index: 2);

    [Fact]
    public Task TestWithExistingField3()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                void M()
                {
                    new [|D|](1);
                }
            }

            class B
            {
                protected int v;
            }

            class D : B
            {
            }
            """,
            """
            class C
            {
                void M()
                {
                    new D(1);
                }
            }

            class B
            {
                protected int v;
            }

            class D : B
            {
                public D(int v)
                {
                    this.v = v;
                }
            }
            """);

    [Fact]
    public Task TestWithExistingField4()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                void M()
                {
                    new [|D|](1);
                }
            }

            class B
            {
                private int v;
            }

            class D : B
            {
            }
            """,
            """
            class C
            {
                void M()
                {
                    new D(1);
                }
            }

            class B
            {
                private int v;
            }

            class D : B
            {
                private int v;

                public D(int v)
                {
                    this.v = v;
                }
            }
            """);

    [Fact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539444")]
    public Task TestWithExistingField5()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                void M(int X)
                {
                    new [|D|](X);
                }
            }

            class D
            {
                int X;
            }
            """,
            """
            class C
            {
                void M(int X)
                {
                    new D(X);
                }
            }

            class D
            {
                int X;

                public D(int x)
                {
                    X = x;
                }
            }
            """);

    [Fact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539444")]
    public Task TestWithExistingField5WithQualification()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                void M(int X)
                {
                    new [|D|](X);
                }
            }

            class D
            {
                int X;
            }
            """,
            """
            class C
            {
                void M(int X)
                {
                    new D(X);
                }
            }

            class D
            {
                int X;

                public D(int x)
                {
                    this.X = x;
                }
            }
            """,
            new(options: Option(CodeStyleOptions2.QualifyFieldAccess, true, NotificationOption2.Error)));

    [Fact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539444")]
    public Task TestWithExistingField6()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                void M(int X)
                {
                    new [|D|](X);
                }
            }

            class B
            {
                private int X;
            }

            class D : B
            {
            }
            """,
            """
            class C
            {
                void M(int X)
                {
                    new D(X);
                }
            }

            class B
            {
                private int X;
            }

            class D : B
            {
                private int x;

                public D(int x)
                {
                    this.x = x;
                }
            }
            """);

    [Fact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539444")]
    public Task TestWithExistingField7()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                void M(int X)
                {
                    new [|D|](X);
                }
            }

            class B
            {
                protected int X;
            }

            class D : B
            {
            }
            """,
            """
            class C
            {
                void M(int X)
                {
                    new D(X);
                }
            }

            class B
            {
                protected int X;
            }

            class D : B
            {
                public D(int x)
                {
                    X = x;
                }
            }
            """);

    [Fact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539444")]
    public Task TestWithExistingField7WithQualification()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                void M(int X)
                {
                    new [|D|](X);
                }
            }

            class B
            {
                protected int X;
            }

            class D : B
            {
            }
            """,
            """
            class C
            {
                void M(int X)
                {
                    new D(X);
                }
            }

            class B
            {
                protected int X;
            }

            class D : B
            {
                public D(int x)
                {
                    this.X = x;
                }
            }
            """,
            new(options: Option(CodeStyleOptions2.QualifyFieldAccess, true, NotificationOption2.Error)));

    [Fact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539444")]
    public Task TestWithExistingField8()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                void M(int X)
                {
                    new [|D|](X);
                }
            }

            class B
            {
                protected static int x;
            }

            class D : B
            {
            }
            """,
            """
            class C
            {
                void M(int X)
                {
                    new D(X);
                }
            }

            class B
            {
                protected static int x;
            }

            class D : B
            {
                private int x1;

                public D(int x1)
                {
                    this.x1 = x1;
                }
            }
            """);

    [Fact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539444")]
    public Task TestWithExistingField9()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                void M(int X)
                {
                    new [|D|](X);
                }
            }

            class B
            {
                protected int x;
            }

            class D : B
            {
                int X;
            }
            """,
            """
            class C
            {
                void M(int X)
                {
                    new D(X);
                }
            }

            class B
            {
                protected int x;
            }

            class D : B
            {
                int X;

                public D(int x)
                {
                    this.x = x;
                }
            }
            """);

    [Fact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539444")]
    public Task TestWithExistingProperty1()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                void M(int X)
                {
                    new [|D|](X);
                }
            }

            class D
            {
                public int X { get; private set; }
            }
            """,
            """
            class C
            {
                void M(int X)
                {
                    new D(X);
                }
            }

            class D
            {
                public D(int x)
                {
                    X = x;
                }

                public int X { get; private set; }
            }
            """);

    [Fact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539444")]
    public Task TestWithExistingProperty1WithQualification()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                void M(int X)
                {
                    new [|D|](X);
                }
            }

            class D
            {
                public int X { get; private set; }
            }
            """,
            """
            class C
            {
                void M(int X)
                {
                    new D(X);
                }
            }

            class D
            {
                public D(int x)
                {
                    this.X = x;
                }

                public int X { get; private set; }
            }
            """,
            new(options: Option(CodeStyleOptions2.QualifyPropertyAccess, true, NotificationOption2.Error)));

    [Fact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539444")]
    public Task TestWithExistingProperty2()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                void M(int X)
                {
                    new [|D|](X);
                }
            }

            class B
            {
                public int X { get; private set; }
            }

            class D : B
            {
            }
            """,
            """
            class C
            {
                void M(int X)
                {
                    new D(X);
                }
            }

            class B
            {
                public int X { get; private set; }
            }

            class D : B
            {
                private int x;

                public D(int x)
                {
                    this.x = x;
                }
            }
            """);

    [Fact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539444")]
    public Task TestWithExistingProperty3()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                void M(int X)
                {
                    new [|D|](X);
                }
            }

            class B
            {
                public int X { get; protected set; }
            }

            class D : B
            {
            }
            """,
            """
            class C
            {
                void M(int X)
                {
                    new D(X);
                }
            }

            class B
            {
                public int X { get; protected set; }
            }

            class D : B
            {
                public D(int x)
                {
                    X = x;
                }
            }
            """);

    [Fact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539444")]
    public Task TestWithExistingProperty3WithQualification()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                void M(int X)
                {
                    new [|D|](X);
                }
            }

            class B
            {
                public int X { get; protected set; }
            }

            class D : B
            {
            }
            """,
            """
            class C
            {
                void M(int X)
                {
                    new D(X);
                }
            }

            class B
            {
                public int X { get; protected set; }
            }

            class D : B
            {
                public D(int x)
                {
                    this.X = x;
                }
            }
            """,
            new(options: Option(CodeStyleOptions2.QualifyPropertyAccess, true, NotificationOption2.Error)));

    [Fact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539444")]
    public Task TestWithExistingProperty4()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                void M(int X)
                {
                    new [|D|](X);
                }
            }

            class B
            {
                protected int X { get; set; }
            }

            class D : B
            {
            }
            """,
            """
            class C
            {
                void M(int X)
                {
                    new D(X);
                }
            }

            class B
            {
                protected int X { get; set; }
            }

            class D : B
            {
                public D(int x)
                {
                    X = x;
                }
            }
            """);

    [Fact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539444")]
    public Task TestWithExistingProperty4WithQualification()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                void M(int X)
                {
                    new [|D|](X);
                }
            }

            class B
            {
                protected int X { get; set; }
            }

            class D : B
            {
            }
            """,
            """
            class C
            {
                void M(int X)
                {
                    new D(X);
                }
            }

            class B
            {
                protected int X { get; set; }
            }

            class D : B
            {
                public D(int x)
                {
                    this.X = x;
                }
            }
            """,
            new(options: Option(CodeStyleOptions2.QualifyPropertyAccess, true, NotificationOption2.Error)));

    [Fact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539444")]
    public Task TestWithExistingProperty5()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                void M(int X)
                {
                    new [|D|](X);
                }
            }

            class B
            {
                protected int X { get; }
            }

            class D : B
            {
            }
            """,
            """
            class C
            {
                void M(int X)
                {
                    new D(X);
                }
            }

            class B
            {
                protected int X { get; }
            }

            class D : B
            {
                private int x;

                public D(int x)
                {
                    this.x = x;
                }
            }
            """);

    [Fact]
    public Task TestWithOutParam()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                void M(int i)
                {
                    new [|D|](out i);
                }
            }

            class D
            {
            }
            """,
            """
            class C
            {
                void M(int i)
                {
                    new D(out i);
                }
            }

            class D
            {
                public D(out int i)
                {
                    i = 0;
                }
            }
            """);

    [Fact]
    public async Task TestWithBaseDelegatingConstructor1()
    {
        const string input =
            """
            class C
            {
                void M()
                {
                    new [|D|](1);
                }
            }

            class B
            {
                protected B(int x)
                {
                }
            }

            class D : B
            {
            }
            """;

        await TestActionCountAsync(input, 1);
        await TestInRegularAndScriptAsync(
     input,
     """
     class C
     {
         void M()
         {
             new D(1);
         }
     }

     class B
     {
         protected B(int x)
         {
         }
     }

     class D : B
     {
         public D(int x) : base(x)
         {
         }
     }
     """);
    }

    [Fact]
    public Task TestWithBaseDelegatingConstructor2()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                void M()
                {
                    new [|D|](1);
                }
            }

            class B
            {
                private B(int x)
                {
                }
            }

            class D : B
            {
            }
            """,
            """
            class C
            {
                void M()
                {
                    new D(1);
                }
            }

            class B
            {
                private B(int x)
                {
                }
            }

            class D : B
            {
                private int v;

                public D(int v)
                {
                    this.v = v;
                }
            }
            """);

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/44537")]
    public Task TestWithBaseDelegatingConstructor2_WithProperties()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                void M()
                {
                    new [|D|](1);
                }
            }

            class B
            {
                private B(int x)
                {
                }
            }

            class D : B
            {
            }
            """,
            """
            class C
            {
                void M()
                {
                    new D(1);
                }
            }

            class B
            {
                private B(int x)
                {
                }
            }

            class D : B
            {
                public D(int v)
                {
                    V = v;
                }

                public int V { get; }
            }
            """, index: 1);

    [Fact]
    public Task TestWithBaseDelegatingConstructor2_NoMembers()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                void M()
                {
                    new [|D|](1);
                }
            }

            class B
            {
                private B(int x)
                {
                }
            }

            class D : B
            {
            }
            """,
            """
            class C
            {
                void M()
                {
                    new D(1);
                }
            }

            class B
            {
                private B(int x)
                {
                }
            }

            class D : B
            {
                public D(int v)
                {
                }
            }
            """, index: 2);

    [Fact]
    public Task TestStructInLocalInitializerWithSystemType()
        => TestInRegularAndScriptAsync(
            """
            struct S
            {
                void M()
                {
                    S s = new [|S|](System.DateTime.Now);
                }
            }
            """,
            """
            using System;

            struct S
            {
                private DateTime now;

                public S(DateTime now)
                {
                    this.now = now;
                }

                void M()
                {
                    S s = new S(System.DateTime.Now);
                }
            }
            """);

    [Fact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539489")]
    public Task TestEscapedName()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                void M()
                {
                    new [|@C|](1);
                }
            }
            """,
            """
            class C
            {
                private int v;

                public C(int v)
                {
                    this.v = v;
                }

                void M()
                {
                    new @C(1);
                }
            }
            """);

    [Fact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539489")]
    public Task TestEscapedKeyword()
        => TestInRegularAndScriptAsync(
            """
            class @int
            {
                void M()
                {
                    new [|@int|](1);
                }
            }
            """,
            """
            class @int
            {
                private int v;

                public @int(int v)
                {
                    this.v = v;
                }

                void M()
                {
                    new @int(1);
                }
            }
            """);

    [Fact]
    public Task TestIsSymbolAccessibleWithInternalField()
        => TestInRegularAndScriptAsync(
            """
            class Base
            {
                internal long field;

                void Main()
                {
                    int field = 5;
                    new [|Derived|](field);
                }
            }

            class Derived : Base
            {
            }
            """,
            """
            class Base
            {
                internal long field;

                void Main()
                {
                    int field = 5;
                    new Derived(field);
                }
            }

            class Derived : Base
            {
                public Derived(int field)
                {
                    this.field = field;
                }
            }
            """);

    [Fact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539548")]
    public Task TestFormatting()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                void M()
                {
                    new [|C|](1);
                }
            }
            """,
            """
            class C
            {
                private int v;

                public C(int v)
                {
                    this.v = v;
                }

                void M()
                {
                    new C(1);
                }
            }
            """);

    [Fact, WorkItem(5864, "DevDiv_Projects/Roslyn")]
    public Task TestNotOnStructConstructor()
        => TestMissingInRegularAndScriptAsync(
            """
            struct Struct
            {
                void Main()
                {
                    Struct s = new [|Struct|]();
                }
            }
            """);

    [Fact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539787")]
    public Task TestGenerateIntoCorrectPart()
        => TestInRegularAndScriptAsync(
            """
            partial class C
            {
            }

            partial class C
            {
                void Method()
                {
                    C c = new [|C|]("a");
                }
            }
            """,
            """
            partial class C
            {
            }

            partial class C
            {
                private string v;

                public C(string v)
                {
                    this.v = v;
                }

                void Method()
                {
                    C c = new C("a");
                }
            }
            """);

    [Fact]
    public Task TestDelegateToSmallerConstructor1()
        => TestInRegularAndScriptAsync(
            """
            class A
            {
                void M()
                {
                    Delta d1 = new Delta("ss", 3);
                    Delta d2 = new [|Delta|]("ss", 5, true);
                }
            }

            class Delta
            {
                private string v1;
                private int v2;

                public Delta(string v1, int v2)
                {
                    this.v1 = v1;
                    this.v2 = v2;
                }
            }
            """,
            """
            class A
            {
                void M()
                {
                    Delta d1 = new Delta("ss", 3);
                    Delta d2 = new Delta("ss", 5, true);
                }
            }

            class Delta
            {
                private string v1;
                private int v2;
                private bool v;

                public Delta(string v1, int v2)
                {
                    this.v1 = v1;
                    this.v2 = v2;
                }

                public Delta(string v1, int v2, bool v) : this(v1, v2)
                {
                    this.v = v;
                }
            }
            """);

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/44537")]
    public Task TestDelegateToSmallerConstructor1_WithProperties()
        => TestInRegularAndScriptAsync(
            """
            class A
            {
                void M()
                {
                    Delta d1 = new Delta("ss", 3);
                    Delta d2 = new [|Delta|]("ss", 5, true);
                }
            }

            class Delta
            {
                private string v1;
                private int v2;

                public Delta(string v1, int v2)
                {
                    this.v1 = v1;
                    this.v2 = v2;
                }
            }
            """,
            """
            class A
            {
                void M()
                {
                    Delta d1 = new Delta("ss", 3);
                    Delta d2 = new Delta("ss", 5, true);
                }
            }

            class Delta
            {
                private string v1;
                private int v2;

                public Delta(string v1, int v2)
                {
                    this.v1 = v1;
                    this.v2 = v2;
                }

                public Delta(string v1, int v2, bool v) : this(v1, v2)
                {
                    V = v;
                }

                public bool V { get; }
            }
            """, index: 1);

    [Fact]
    public Task TestDelegateToSmallerConstructor1_NoMembers()
        => TestInRegularAndScriptAsync(
            """
            class A
            {
                void M()
                {
                    Delta d1 = new Delta("ss", 3);
                    Delta d2 = new [|Delta|]("ss", 5, true);
                }
            }

            class Delta
            {
                private string v1;
                private int v2;

                public Delta(string v1, int v2)
                {
                    this.v1 = v1;
                    this.v2 = v2;
                }
            }
            """,
            """
            class A
            {
                void M()
                {
                    Delta d1 = new Delta("ss", 3);
                    Delta d2 = new Delta("ss", 5, true);
                }
            }

            class Delta
            {
                private string v1;
                private int v2;

                public Delta(string v1, int v2)
                {
                    this.v1 = v1;
                    this.v2 = v2;
                }

                public Delta(string v1, int v2, bool v) : this(v1, v2)
                {
                }
            }
            """, index: 2);

    [Fact]
    public Task TestDelegateToSmallerConstructor2()
        => TestInRegularAndScriptAsync(
            """
            class A
            {
                void M()
                {
                    Delta d1 = new Delta("ss", 3);
                    Delta d2 = new [|Delta|]("ss", 5, true);
                }
            }

            class Delta
            {
                private string a;
                private int b;

                public Delta(string a, int b)
                {
                    this.a = a;
                    this.b = b;
                }
            }
            """,
            """
            class A
            {
                void M()
                {
                    Delta d1 = new Delta("ss", 3);
                    Delta d2 = new Delta("ss", 5, true);
                }
            }

            class Delta
            {
                private string a;
                private int b;
                private bool v;

                public Delta(string a, int b)
                {
                    this.a = a;
                    this.b = b;
                }

                public Delta(string a, int b, bool v) : this(a, b)
                {
                    this.v = v;
                }
            }
            """);

    [Fact]
    public Task TestDelegateToSmallerConstructor3()
        => TestInRegularAndScriptAsync(
            """
            class A
            {
                void M()
                {
                    var d1 = new Base("ss", 3);
                    var d2 = new [|Delta|]("ss", 5, true);
                }
            }

            class Base
            {
                private string v1;
                private int v2;

                public Base(string v1, int v2)
                {
                    this.v1 = v1;
                    this.v2 = v2;
                }
            }

            class Delta : Base
            {
            }
            """,
            """
            class A
            {
                void M()
                {
                    var d1 = new Base("ss", 3);
                    var d2 = new Delta("ss", 5, true);
                }
            }

            class Base
            {
                private string v1;
                private int v2;

                public Base(string v1, int v2)
                {
                    this.v1 = v1;
                    this.v2 = v2;
                }
            }

            class Delta : Base
            {
                private bool v;

                public Delta(string v1, int v2, bool v) : base(v1, v2)
                {
                    this.v = v;
                }
            }
            """);

    [Fact]
    public Task TestDelegateToSmallerConstructor4()
        => TestInRegularAndScriptAsync(
            """
            class A
            {
                void M()
                {
                    Delta d1 = new Delta("ss", 3);
                    Delta d2 = new [|Delta|]("ss", 5, true);
                }
            }

            class Delta
            {
                private string v1;
                private int v2;

                public Delta(string v1, int v2)
                {
                    this.v1 = v1;
                    this.v2 = v2;
                }
            }
            """,
            """
            class A
            {
                void M()
                {
                    Delta d1 = new Delta("ss", 3);
                    Delta d2 = new Delta("ss", 5, true);
                }
            }

            class Delta
            {
                private string v1;
                private int v2;
                private bool v;

                public Delta(string v1, int v2)
                {
                    this.v1 = v1;
                    this.v2 = v2;
                }

                public Delta(string v1, int v2, bool v) : this(v1, v2)
                {
                    this.v = v;
                }
            }
            """);

    [Fact]
    public Task TestGenerateFromThisInitializer1()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                public C() [|: this(4)|]
                {
                }
            }
            """,
            """
            class C
            {
                private int v;

                public C() : this(4)
                {
                }

                public C(int v)
                {
                    this.v = v;
                }
            }
            """);

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/44537")]
    public Task TestGenerateFromThisInitializer1_WithProperties()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                public C() [|: this(4)|]
                {
                }
            }
            """,
            """
            class C
            {
                public C() : this(4)
                {
                }

                public C(int v)
                {
                    V = v;
                }

                public int V { get; }
            }
            """, index: 1);

    [Fact]
    public Task TestGenerateFromThisInitializer1_NoMembers()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                public C() [|: this(4)|]
                {
                }
            }
            """,
            """
            class C
            {
                public C() : this(4)
                {
                }

                public C(int v)
                {
                }
            }
            """, index: 2);

    [Fact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/910589")]
    public Task TestGenerateFromThisInitializer2()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                public C(int i) [|: this()|]
                {
                }
            }
            """,
            """
            class C
            {
                public C()
                {
                }

                public C(int i) : this()
                {
                }
            }
            """);

    [Fact]
    public Task TestGenerateFromBaseInitializer1()
        => TestInRegularAndScriptAsync(
            """
            class C : B
            {
                public C(int i) [|: base(i)|]
                {
                }
            }

            class B
            {
            }
            """,
            """
            class C : B
            {
                public C(int i) : base(i)
                {
                }
            }

            class B
            {
                private int i;

                public B(int i)
                {
                    this.i = i;
                }
            }
            """);

    [Fact]
    public Task TestGenerateFromBaseInitializer2()
        => TestInRegularAndScriptAsync(
            """
            class C : B
            {
                public C(int i) [|: base(i)|]
                {
                }
            }

            class B
            {
                int i;
            }
            """,
            """
            class C : B
            {
                public C(int i) : base(i)
                {
                }
            }

            class B
            {
                int i;

                public B(int i)
                {
                    this.i = i;
                }
            }
            """);

    [Fact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539969")]
    public Task TestNotOnExistingConstructor()
        => TestMissingInRegularAndScriptAsync(
            """
            class C
            {
                private class D
                {
                }
            }

            class A
            {
                void M()
                {
                    C.D d = new C.[|D|]();
                }
            }
            """);

    [Fact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539972")]
    public Task TestUnavailableTypeParameters()
        => TestInRegularAndScriptAsync(
            """
            class C<T1, T2>
            {
                public void Goo(T1 t1, T2 t2)
                {
                    A a = new [|A|](t1, t2);
                }
            }

            internal class A
            {
            }
            """,
            """
            class C<T1, T2>
            {
                public void Goo(T1 t1, T2 t2)
                {
                    A a = new A(t1, t2);
                }
            }

            internal class A
            {
                private object t1;
                private object t2;

                public A(object t1, object t2)
                {
                    this.t1 = t1;
                    this.t2 = t2;
                }
            }
            """);

    [Fact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539972")]
    public Task TestUnavailableTypeParameters_WithProperties()
        => TestInRegularAndScriptAsync(
            """
            class C<T1, T2>
            {
                public void Goo(T1 t1, T2 t2)
                {
                    A a = new [|A|](t1, t2);
                }
            }

            internal class A
            {
            }
            """,
            """
            class C<T1, T2>
            {
                public void Goo(T1 t1, T2 t2)
                {
                    A a = new A(t1, t2);
                }
            }

            internal class A
            {
                public A(object t1, object t2)
                {
                    T1 = t1;
                    T2 = t2;
                }

                public object T1 { get; }
                public object T2 { get; }
            }
            """, index: 1);

    [Fact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539972")]
    public Task TestUnavailableTypeParameters_NoMembers()
        => TestInRegularAndScriptAsync(
            """
            class C<T1, T2>
            {
                public void Goo(T1 t1, T2 t2)
                {
                    A a = new [|A|](t1, t2);
                }
            }

            internal class A
            {
            }
            """,
            """
            class C<T1, T2>
            {
                public void Goo(T1 t1, T2 t2)
                {
                    A a = new A(t1, t2);
                }
            }

            internal class A
            {
                public A(object t1, object t2)
                {
                }
            }
            """, index: 2);

    [Fact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/541020")]
    public Task TestGenerateCallToDefaultConstructorInStruct()
        => TestInRegularAndScriptAsync(
            """
            class Program
            {
                void Main()
                {
                    Apartment Metropolitan = new Apartment([|"Pine"|]);
                }
            }

            struct Apartment
            {
                private int v1;

                public Apartment(int v1)
                {
                    this.v1 = v1;
                }
            }
            """,
            """
            class Program
            {
                void Main()
                {
                    Apartment Metropolitan = new Apartment("Pine");
                }
            }

            struct Apartment
            {
                private int v1;
                private string v;

                public Apartment(int v1)
                {
                    this.v1 = v1;
                }

                public Apartment(string v) : this()
                {
                    this.v = v;
                }
            }
            """);

    [Fact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/541121")]
    public Task TestReadonlyFieldDelegation()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                private readonly int x;

                void Test()
                {
                    int x = 10;
                    C c = new [|C|](x);
                }
            }
            """,
            """
            class C
            {
                private readonly int x;

                public C(int x)
                {
                    this.x = x;
                }

                void Test()
                {
                    int x = 10;
                    C c = new C(x);
                }
            }
            """);

    [Fact]
    public Task TestNoGenerationIntoEntirelyHiddenType()
        => TestMissingInRegularAndScriptAsync(
            """
            class C
            {
                void Goo()
                {
                    new [|D|](1, 2, 3);
                }
            }

            #line hidden
            class D
            {
            }
            #line default
            """);

    [Fact]
    public Task TestNestedConstructorCall()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                void Goo()
                {
                    var d = new D([|v|]: new D(u: 1));
                }
            }

            class D
            {
                private int u;

                public D(int u)
                {
                }
            }
            """,
            """
            class C
            {
                void Goo()
                {
                    var d = new D(v: new D(u: 1));
                }
            }

            class D
            {
                private int u;
                private D v;

                public D(int u)
                {
                }

                public D(D v)
                {
                    this.v = v;
                }
            }
            """);

    [Fact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/530003")]
    public Task TestAttributesWithArgument()
        => TestInRegularAndScriptAsync(
            """
            using System;

            [AttributeUsage(AttributeTargets.Class)]
            class MyAttribute : Attribute
            {
            }

            [[|MyAttribute(123)|]]
            class D
            {
            }
            """,
            """
            using System;

            [AttributeUsage(AttributeTargets.Class)]
            class MyAttribute : Attribute
            {
                private int v;

                public MyAttribute(int v)
                {
                    this.v = v;
                }
            }

            [MyAttribute(123)]
            class D
            {
            }
            """);

    [Fact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/530003")]
    public Task TestAttributesWithArgument_WithProperties()
        => TestInRegularAndScriptAsync(
            """
            using System;

            [AttributeUsage(AttributeTargets.Class)]
            class MyAttribute : Attribute
            {
            }

            [[|MyAttribute(123)|]]
            class D
            {
            }
            """,
            """
            using System;

            [AttributeUsage(AttributeTargets.Class)]
            class MyAttribute : Attribute
            {
                public MyAttribute(int v)
                {
                    V = v;
                }

                public int V { get; }
            }

            [MyAttribute(123)]
            class D
            {
            }
            """, index: 1);

    [Fact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/530003")]
    public Task TestAttributesWithArgument_NoMembers()
        => TestInRegularAndScriptAsync(
            """
            using System;

            [AttributeUsage(AttributeTargets.Class)]
            class MyAttribute : Attribute
            {
            }

            [[|MyAttribute(123)|]]
            class D
            {
            }
            """,
            """
            using System;

            [AttributeUsage(AttributeTargets.Class)]
            class MyAttribute : Attribute
            {
                public MyAttribute(int v)
                {
                }
            }

            [MyAttribute(123)]
            class D
            {
            }
            """, index: 2);

    [Fact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/530003")]
    public Task TestAttributesWithMultipleArguments()
        => TestInRegularAndScriptAsync(
            """
            using System;

            [AttributeUsage(AttributeTargets.Class)]
            class MyAttribute : Attribute
            {
            }

            [[|MyAttribute(true, 1, "hello")|]]
            class D
            {
            }
            """,
            """
            using System;

            [AttributeUsage(AttributeTargets.Class)]
            class MyAttribute : Attribute
            {
                private bool v1;
                private int v2;
                private string v3;

                public MyAttribute(bool v1, int v2, string v3)
                {
                    this.v1 = v1;
                    this.v2 = v2;
                    this.v3 = v3;
                }
            }

            [MyAttribute(true, 1, "hello")]
            class D
            {
            }
            """);

    [Fact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/530003")]
    public Task TestAttributesWithNamedArguments()
        => TestInRegularAndScriptAsync(
            """
            using System;

            [AttributeUsage(AttributeTargets.Class)]
            class MyAttribute : Attribute
            {
            }

            [[|MyAttribute(true, 1, topic = "hello")|]]
            class D
            {
            }
            """,
            """
            using System;

            [AttributeUsage(AttributeTargets.Class)]
            class MyAttribute : Attribute
            {
                private bool v1;
                private int v2;
                private string topic;

                public MyAttribute(bool v1, int v2, string topic)
                {
                    this.v1 = v1;
                    this.v2 = v2;
                    this.topic = topic;
                }
            }

            [MyAttribute(true, 1, topic = "hello")]
            class D
            {
            }
            """);

    [Fact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/530003")]
    public Task TestAttributesWithAdditionalConstructors()
        => TestInRegularAndScriptAsync(
            """
            using System;

            [AttributeUsage(AttributeTargets.Class)]
            class MyAttribute : Attribute
            {
                private int v;

                public MyAttribute(int v)
                {
                    this.v = v;
                }
            }

            [[|MyAttribute(true, 1)|]]
            class D
            {
            }
            """,
            """
            using System;

            [AttributeUsage(AttributeTargets.Class)]
            class MyAttribute : Attribute
            {
                private int v;
                private bool v1;
                private int v2;

                public MyAttribute(int v)
                {
                    this.v = v;
                }

                public MyAttribute(bool v1, int v2)
                {
                    this.v1 = v1;
                    this.v2 = v2;
                }
            }

            [MyAttribute(true, 1)]
            class D
            {
            }
            """);

    [Fact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/530003")]
    public Task TestAttributesWithOverloading()
        => TestInRegularAndScriptAsync(
            """
            using System;

            [AttributeUsage(AttributeTargets.Class)]
            class MyAttribute : Attribute
            {
                private int v;

                public MyAttribute(int v)
                {
                    this.v = v;
                }
            }

            [[|MyAttribute(true)|]]
            class D
            {
            }
            """,
            """
            using System;

            [AttributeUsage(AttributeTargets.Class)]
            class MyAttribute : Attribute
            {
                private int v;
                private bool v1;

                public MyAttribute(int v)
                {
                    this.v = v;
                }

                public MyAttribute(bool v1)
                {
                    this.v1 = v1;
                }
            }

            [MyAttribute(true)]
            class D
            {
            }
            """);

    [Fact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/530003")]
    public Task TestAttributesWithOverloadingMultipleParameters()
        => TestInRegularAndScriptAsync(
            """
            using System;

            [AttributeUsage(AttributeTargets.Class)]
            class MyAttrAttribute : Attribute
            {
                private bool v1;
                private int v2;

                public MyAttrAttribute(bool v1, int v2)
                {
                    this.v1 = v1;
                    this.v2 = v2;
                }
            }

            [|[MyAttrAttribute(1, true)]|]
            class D
            {
            }
            """,
            """
            using System;

            [AttributeUsage(AttributeTargets.Class)]
            class MyAttrAttribute : Attribute
            {
                private bool v1;
                private int v2;
                private int v;
                private bool v3;

                public MyAttrAttribute(bool v1, int v2)
                {
                    this.v1 = v1;
                    this.v2 = v2;
                }

                public MyAttrAttribute(int v, bool v3)
                {
                    this.v = v;
                    this.v3 = v3;
                }
            }

            [MyAttrAttribute(1, true)]
            class D
            {
            }
            """);

    [Fact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/530003")]
    public Task TestAttributesWithDelegation()
        => TestMissingInRegularAndScriptAsync(
            """
            using System;

            [AttributeUsage(AttributeTargets.Class)]
            class MyAttrAttribute : Attribute
            {
            }

            [|[MyAttrAttribute(() => {
                return;
            })]|]
            class D
            {
            }
            """);

    [Fact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/530003")]
    public Task TestAttributesWithLambda()
        => TestMissingInRegularAndScriptAsync(
            """
            using System;

            [AttributeUsage(AttributeTargets.Class)]
            class MyAttrAttribute : Attribute
            {
            }

            [|[MyAttrAttribute(() => 5)]|]
            class D
            {
            }
            """);

    [Fact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/889349")]
    public Task TestConstructorGenerationForDifferentNamedParameter()
        => TestInRegularAndScriptAsync(
            """
            class Program
            {
                static void Main(string[] args)
                {
                    var ss = new [|Program(wde: 1)|];
                }

                Program(int s)
                {

                }
            }
            """,
            """
            class Program
            {
                private int wde;

                static void Main(string[] args)
                {
                    var ss = new Program(wde: 1);
                }

                Program(int s)
                {

                }

                public Program(int wde)
                {
                    this.wde = wde;
                }
            }
            """);

    [Fact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/528257")]
    public Task TestGenerateInInaccessibleType()
        => TestInRegularAndScriptAsync(
            """
            class Goo
            {
                class Bar
                {
                }
            }

            class A
            {
                static void Main(string[] args)
                {
                    var s = new [|Goo.Bar(5)|];
                }
            }
            """,
            """
            class Goo
            {
                class Bar
                {
                    private int v;

                    public Bar(int v)
                    {
                        this.v = v;
                    }
                }
            }

            class A
            {
                static void Main(string[] args)
                {
                    var s = new Goo.Bar(5);
                }
            }
            """);

    [Fact, WorkItem(1241, @"https://github.com/dotnet/roslyn/issues/1241")]
    public Task TestGenerateConstructorInIncompleteLambda()
        => TestInRegularAndScriptAsync(
            """
            using System.Threading.Tasks;

            class C
            {
                C()
                {
                    Task.Run(() => {
                        new [|C|](0) });
                }
            }
            """,
            """
            using System.Threading.Tasks;

            class C
            {
                private int v;

                public C(int v)
                {
                    this.v = v;
                }

                C()
                {
                    Task.Run(() => {
                        new C(0) });
                }
            }
            """);

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/5274")]
    public Task TestGenerateIntoDerivedClassWithAbstractBase()
        => TestInRegularAndScriptAsync(
            """
            class Class1
            {
                private void Goo(string value)
                {
                    var rewriter = new [|Derived|](value);
                }

                private class Derived : Base
                {
                }

                public abstract partial class Base
                {
                    private readonly bool _val;

                    public Base(bool val = false)
                    {
                        _val = val;
                    }
                }
            }
            """,
            """
            class Class1
            {
                private void Goo(string value)
                {
                    var rewriter = new Derived(value);
                }

                private class Derived : Base
                {
                    private string value;

                    public Derived(string value)
                    {
                        this.value = value;
                    }
                }

                public abstract partial class Base
                {
                    private readonly bool _val;

                    public Base(bool val = false)
                    {
                        _val = val;
                    }
                }
            }
            """);

    [Fact]
    public Task TestGenerateWithIncorrectConstructorArguments_Crash()
        => TestInRegularAndScriptAsync(
            """
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using System.Threading.Tasks;

            abstract class Y
            {
                class X : Y
                {
                    void M()
                    {
                        new X(new [|string|]());
                    }
                }
            }
            """,
            """
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using System.Threading.Tasks;

            abstract class Y
            {
                class X : Y
                {
                    private string v;

                    public X(string v)
                    {
                        this.v = v;
                    }

                    void M()
                    {
                        new X(new string());
                    }
                }
            }
            """);

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/9575")]
    public Task TestMissingOnMethodCall()
        => TestMissingInRegularAndScriptAsync(
            """
            class C
            {
                public C(int arg)
                {
                }

                public bool M(string s, int i, bool b)
                {
                    return [|M|](i, b);
                }
            }
            """);

    [Fact]
    public Task Tuple()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                void M()
                {
                    new [|C|]((1, "hello"), true);
                }
            }
            """,
            """
            class C
            {
                private (int, string) value;
                private bool v;

                public C((int, string) value, bool v)
                {
                    this.value = value;
                    this.v = v;
                }

                void M()
                {
                    new C((1, "hello"), true);
                }
            }
            """);

    [Fact]
    public Task TupleWithNames()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                void M()
                {
                    new [|C|]((a: 1, b: "hello"));
                }
            }
            """,
            """
            class C
            {
                private (int a, string b) value;

                public C((int a, string b) value)
                {
                    this.value = value;
                }

                void M()
                {
                    new C((a: 1, b: "hello"));
                }
            }
            """);

    [Fact]
    public Task TupleWithOneName()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                void M()
                {
                    new [|C|]((a: 1, "hello"));
                }
            }
            """,
            """
            class C
            {
                private (int a, string) value;

                public C((int a, string) value)
                {
                    this.value = value;
                }

                void M()
                {
                    new C((a: 1, "hello"));
                }
            }
            """);

    [Fact]
    public Task TupleAndExistingField()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                void M()
                {
                    new [|D(existing: (1, "hello"))|];
                }
            }

            class D
            {
                private (int, string) existing;
            }
            """,
            """
            class C
            {
                void M()
                {
                    new D(existing: (1, "hello"));
                }
            }

            class D
            {
                private (int, string) existing;

                public D((int, string) existing)
                {
                    this.existing = existing;
                }
            }
            """);

    [Fact]
    public Task TupleWithNamesAndExistingField()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                void M()
                {
                    new [|D(existing: (a: 1, b: "hello"))|];
                }
            }

            class D
            {
                private (int a, string b) existing;
            }
            """,
            """
            class C
            {
                void M()
                {
                    new D(existing: (a: 1, b: "hello"));
                }
            }

            class D
            {
                private (int a, string b) existing;

                public D((int a, string b) existing)
                {
                    this.existing = existing;
                }
            }
            """);

    [Fact]
    public Task TupleWithDifferentNamesAndExistingField()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                void M()
                {
                    new [|D(existing: (a: 1, b: "hello"))|];
                }
            }

            class D
            {
                private (int c, string d) existing;
            }
            """,
            """
            class C
            {
                void M()
                {
                    new D(existing: (a: 1, b: "hello"));
                }
            }

            class D
            {
                private (int c, string d) existing;

                public D((int a, string b) existing)
                {
                    this.existing = existing;
                }
            }
            """);

    [Fact]
    public Task TupleAndDelegatingConstructor()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                void M()
                {
                    new [|D|]((1, "hello"));
                }
            }

            class B
            {
                protected B((int, string) x)
                {
                }
            }

            class D : B
            {
            }
            """,
            """
            class C
            {
                void M()
                {
                    new D((1, "hello"));
                }
            }

            class B
            {
                protected B((int, string) x)
                {
                }
            }

            class D : B
            {
                public D((int, string) x) : base(x)
                {
                }
            }
            """);

    [Fact]
    public Task TupleWithNamesAndDelegatingConstructor()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                void M()
                {
                    new [|D|]((a: 1, b: "hello"));
                }
            }

            class B
            {
                protected B((int a, string b) x)
                {
                }
            }

            class D : B
            {
            }
            """,
            """
            class C
            {
                void M()
                {
                    new D((a: 1, b: "hello"));
                }
            }

            class B
            {
                protected B((int a, string b) x)
                {
                }
            }

            class D : B
            {
                public D((int a, string b) x) : base(x)
                {
                }
            }
            """);

    [Fact]
    public Task TupleWithDifferentNamesAndDelegatingConstructor()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                void M()
                {
                    new [|D|]((a: 1, b: "hello"));
                }
            }

            class B
            {
                protected B((int c, string d) x)
                {
                }
            }

            class D : B
            {
            }
            """,
            """
            class C
            {
                void M()
                {
                    new D((a: 1, b: "hello"));
                }
            }

            class B
            {
                protected B((int c, string d) x)
                {
                }
            }

            class D : B
            {
                public D((int c, string d) x) : base(x)
                {
                }
            }
            """);

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/11563")]
    [WorkItem("https://github.com/dotnet/roslyn/issues/14077")]
    public Task StripUnderscoresFromParameterNames()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                int _i;
                string _s;

                void M()
                {
                    new [|D|](_i, _s);
                }
            }

            class D
            {
            }
            """,
            """
            class C
            {
                int _i;
                string _s;

                void M()
                {
                    new D(_i, _s);
                }
            }

            class D
            {
                private int i;
                private string s;

                public D(int i, string s)
                {
                    this.i = i;
                    this.s = s;
                }
            }
            """);

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/11563")]
    public Task DoNotStripSingleUnderscore()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                int _;

                void M()
                {
                    new [|D|](_);
                }
            }

            class D
            {
            }
            """,
            """
            class C
            {
                int _;

                void M()
                {
                    new D(_);
                }
            }

            class D
            {
                private int _;

                public D(int _)
                {
                    this._ = _;
                }
            }
            """);

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/12147")]
    public Task TestOutVariableDeclaration_ImplicitlyTyped()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                void M()
                {
                    new [|C|](out var a);
                }
            }
            """,
            """
            class C
            {
                public C(out object a)
                {
                    a = null;
                }

                void M()
                {
                    new C(out var a);
                }
            }
            """);

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/12147")]
    public Task TestOutVariableDeclaration_ImplicitlyTyped_NamedArgument()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                void M()
                {
                    new C([|b|]: out var a);
                }
            }
            """,
            """
            class C
            {
                public C(out object b)
                {
                    b = null;
                }

                void M()
                {
                    new C(b: out var a);
                }
            }
            """);

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/12147")]
    public Task TestOutVariableDeclaration_ExplicitlyTyped()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                void M()
                {
                    new [|C|](out int a);
                }
            }
            """,
            """
            class C
            {
                public C(out int a)
                {
                    a = 0;
                }

                void M()
                {
                    new C(out int a);
                }
            }
            """);

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/12147")]
    public Task TestOutVariableDeclaration_ExplicitlyTyped_NamedArgument()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                void M()
                {
                    new C([|b|]: out int a);
                }
            }
            """,
            """
            class C
            {
                public C(out int b)
                {
                    b = 0;
                }

                void M()
                {
                    new C(b: out int a);
                }
            }
            """);

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/12182")]
    public Task TestOutVariableDeclaration_ImplicitlyTyped_CSharp6()
        => TestAsync(
            """
            class C
            {
                void M()
                {
                    new [|C|](out var a);
                }
            }
            """,
            """
            class C
            {
                public C(out object a)
                {
                    a = null;
                }

                void M()
                {
                    new C(out var a);
                }
            }
            """,
            new(parseOptions: TestOptions.Regular.WithLanguageVersion(CodeAnalysis.CSharp.LanguageVersion.CSharp6)));

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/12182")]
    public Task TestOutVariableDeclaration_ImplicitlyTyped_NamedArgument_CSharp6()
        => TestAsync(
            """
            class C
            {
                void M()
                {
                    new C([|b|]: out var a);
                }
            }
            """,
            """
            class C
            {
                public C(out object b)
                {
                    b = null;
                }

                void M()
                {
                    new C(b: out var a);
                }
            }
            """,
            new(parseOptions: TestOptions.Regular.WithLanguageVersion(CodeAnalysis.CSharp.LanguageVersion.CSharp6)));

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/12182")]
    public Task TestOutVariableDeclaration_ExplicitlyTyped_CSharp6()
        => TestAsync(
            """
            class C
            {
                void M()
                {
                    new [|C|](out int a);
                }
            }
            """,
            """
            class C
            {
                public C(out int a)
                {
                    a = 0;
                }

                void M()
                {
                    new C(out int a);
                }
            }
            """,
            new(parseOptions: TestOptions.Regular.WithLanguageVersion(CodeAnalysis.CSharp.LanguageVersion.CSharp6)));

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/12182")]
    public Task TestOutVariableDeclaration_ExplicitlyTyped_NamedArgument_CSharp6()
        => TestAsync(
            """
            class C
            {
                void M()
                {
                    new C([|b|]: out int a);
                }
            }
            """,
            """
            class C
            {
                public C(out int b)
                {
                    b = 0;
                }

                void M()
                {
                    new C(b: out int a);
                }
            }
            """,
            new(parseOptions: TestOptions.Regular.WithLanguageVersion(CodeAnalysis.CSharp.LanguageVersion.CSharp6)));

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/13749")]
    public Task Support_Readonly_Properties()
        => TestInRegularAndScriptAsync(
            """
            class C {
                public int Prop { get ; }
            }

            class P { 
                static void M ( ) { 
                    var prop = 42 ;
                    var c = new [|C|] ( prop ) ;
                }
            }
            """,
            """
            class C {
                public C(int prop)
                {
                    Prop = prop;
                }

                public int Prop { get ; }
            }

            class P { 
                static void M ( ) { 
                    var prop = 42 ;
                    var c = new C ( prop ) ;
                }
            }
            """);

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/21692")]
    public Task TestDelegateConstructor1()
        => TestInRegularAndScriptAsync(
            """
            class A
            {
                public A(int a) : [|this(a, 1)|]
                {
                }
            }
            """,
            """
            class A
            {
                private int a;
                private int v;

                public A(int a) : this(a, 1)
                {
                }

                public A(int a, int v)
                {
                    this.a = a;
                    this.v = v;
                }
            }
            """);

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/21692")]
    public Task TestDelegateConstructor2()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                public C(int x) { }

                public C(int x, int y, int z) : [|this(x, y)|] { }
            }
            """,
            """
            class C
            {
                private int y;

                public C(int x) { }

                public C(int x, int y) : this(x)
                {
                    this.y = y;
                }

                public C(int x, int y, int z) : this(x, y) { }
            }
            """);

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/21692")]
    public Task TestDelegateConstructor3()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                public C(int x) : this(x, 0, 0) { }

                public C(int x, int y, int z) : [|this(x, y)|] { }
            }
            """,
            """
            class C
            {
                private int x;
                private int y;

                public C(int x) : this(x, 0, 0) { }

                public C(int x, int y)
                {
                    this.x = x;
                    this.y = y;
                }

                public C(int x, int y, int z) : this(x, y) { }
            }
            """);

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/21692")]
    public Task TestDelegateConstructor4()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                public C(int x) : this(x, 0) { }

                public C(int x, int y) : [|this(x, y, 0)|] { }
            }
            """,
            """
            class C
            {
                private int x;
                private int y;
                private int v;

                public C(int x) : this(x, 0) { }

                public C(int x, int y) : this(x, y, 0) { }

                public C(int x, int y, int v)
                {
                    this.x = x;
                    this.y = y;
                    this.v = v;
                }
            }
            """);

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/21692")]
    public Task TestDelegateConstructor5()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                public C(int a) { }
                public C(bool b, bool a) : this(0, 0) { }
                public C(int i, int i1) : this(true, true) { }
                public C(int x, int y, int z, int e) : [|this(x, y, z)|] { }
            }
            """,
            """
            class C
            {
                private int y;
                private int z;

                public C(int a) { }
                public C(bool b, bool a) : this(0, 0) { }
                public C(int i, int i1) : this(true, true) { }

                public C(int a, int y, int z) : this(a)
                {
                    this.y = y;
                    this.z = z;
                }

                public C(int x, int y, int z, int e) : this(x, y, z) { }
            }
            """);

    [Theory, WorkItem("https://github.com/dotnet/roslyn/issues/22293")]
    [InlineData("void")]
    [InlineData("int")]
    public Task TestMethodGroupWithMissingSystemActionAndFunc(string returnType)
        => TestInRegularAndScriptAsync(
            $$"""
            <Workspace>
                <Project Language="C#" CommonReferencesMinCorlib="true">
                    <Document><![CDATA[class C
            {
                void M()
                {
                    new [|Class|](Method);
                }

                {{returnType}} Method()
                {
                }
            }

            internal class Class
            {
            }]]></Document>
                </Project>
            </Workspace>
            """,
            $$"""
            class C
            {
                void M()
                {
                    new Class(Method);
                }

                {{returnType}} Method()
                {
                }
            }

            internal class Class
            {
                private object method;

                public Class(object method)
                {
                    this.method = method;
                }
            }
            """);

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/14077")]
    public Task TestGenerateFieldNoNamingStyle()
        => TestInRegularAndScriptAsync(
            """
            class Program
            {
                static void Main(string[] args)
                {
                    string s = ";
                    new Prog[||]ram(s);
                }
            }
            """,
            """
            class Program
            {
                private string s;

                public Program(string s)
                {
                    this.s = s;
                }

                static void Main(string[] args)
                {
                    string s = ";
                    new Program(s);
                }
            }
            """);

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/14077")]
    public Task TestGenerateFieldDefaultNamingStyle()
        => TestInRegularAndScriptAsync(
            """
            class Program
            {
                static void Main(string[] args)
                {
                    string S = ";
                    new Prog[||]ram(S);
                }
            }
            """,
            """
            class Program
            {
                private string s;

                public Program(string s)
                {
                    this.s = s;
                }

                static void Main(string[] args)
                {
                    string S = ";
                    new Program(S);
                }
            }
            """);

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/14077")]
    public Task TestGenerateFieldWithNamingStyle()
        => TestInRegularAndScriptAsync(
            """
            class Program
            {
                static void Main(string[] args)
                {
                    string s = ";
                    new Prog[||]ram(s);
                }
            }
            """,
            """
            class Program
            {
                private string _s;

                public Program(string s)
                {
                    _s = s;
                }

                static void Main(string[] args)
                {
                    string s = ";
                    new Program(s);
                }
            }
            """, new(options: options.FieldNamesAreCamelCaseWithUnderscorePrefix));

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/14077")]
    public Task TestFieldWithNamingStyleAlreadyExists()
        => TestInRegularAndScriptAsync(
            """
            class Program
            {
                private string _s;

                static void Main(string[] args)
                {
                    string s = "";
                    new Prog[||]ram(s);
                }
            }
            """,
            """
            class Program
            {
                private string _s;

                public Program(string s)
                {
                    _s = s;
                }

                static void Main(string[] args)
                {
                    string s = "";
                    new Program(s);
                }
            }
            """, new(options: options.FieldNamesAreCamelCaseWithUnderscorePrefix));

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/14077")]
    public Task TestFieldAndParameterNamingStyles()
        => TestInRegularAndScriptAsync(
            """
            class Program
            {
                static void Main(string[] args)
                {
                    string s = "";
                    new Prog[||]ram(s);
                }
            }
            """,
            """
            class Program
            {
                private string _s;

                public Program(string p_s)
                {
                    _s = p_s;
                }

                static void Main(string[] args)
                {
                    string s = "";
                    new Program(s);
                }
            }
            """, new(options: options.MergeStyles(options.FieldNamesAreCamelCaseWithUnderscorePrefix, options.ParameterNamesAreCamelCaseWithPUnderscorePrefix)));

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/14077")]
    public Task TestAttributeArgumentWithNamingRules()
        => TestInRegularAndScriptAsync(
            """
            using System;

            [AttributeUsage(AttributeTargets.Class)]
            class MyAttribute : Attribute
            {
            }

            [[|MyAttribute(123)|]]
            class D
            {
            }
            """,
            """
            using System;

            [AttributeUsage(AttributeTargets.Class)]
            class MyAttribute : Attribute
            {
                private int _v;

                public MyAttribute(int p_v)
                {
                    _v = p_v;
                }
            }

            [MyAttribute(123)]
            class D
            {
            }
            """, new(options: options.MergeStyles(options.FieldNamesAreCamelCaseWithUnderscorePrefix, options.ParameterNamesAreCamelCaseWithPUnderscorePrefix)));

    [Theory, WorkItem("https://github.com/dotnet/roslyn/issues/33673")]
    [InlineData("_s", "s")]
    [InlineData("_S", "s")]
    [InlineData("m_s", "s")]
    [InlineData("m_S", "s")]
    [InlineData("s_s", "s")]
    [InlineData("t_s", "s")]
    public Task GenerateConstructor_ArgumentHasCommonPrefix(string argumentName, string fieldName)
        => TestInRegularAndScriptAsync(
            $$"""
            class Program
            {
                static void Main(string[] args)
                {
                    string {{argumentName}} = ";
                    new Prog[||]ram({{argumentName}});
                }
            }
            """,
            $$"""
            class Program
            {
                private string {{fieldName}};

                public Program(string {{fieldName}})
                {
                    this.{{fieldName}} = {{fieldName}};
                }

                static void Main(string[] args)
                {
                    string {{argumentName}} = ";
                    new Program({{argumentName}});
                }
            }
            """);

    [Fact]
    public Task TestWithTopLevelNullability()
        => TestInRegularAndScriptAsync(
            """
            #nullable enable

            class C
            {
                void M()
                {
                    string? s = null;
                    new [|C|](s);
                }
            }
            """,
            """
            #nullable enable

            class C
            {
                private string? s;

                public C(string? s)
                {
                    this.s = s;
                }

                void M()
                {
                    string? s = null;
                    new C(s);
                }
            }
            """);

    [Fact]
    public Task TestWithNestedNullability()
        => TestInRegularAndScriptAsync(
            """
            #nullable enable

            using System.Collections.Generic;

            class C
            {
                void M()
                {
                    IEnumerable<string?> s;
                    new [|C|](s);
                }
            }
            """,
            """
            #nullable enable

            using System.Collections.Generic;

            class C
            {
                private IEnumerable<string?> s;

                public C(IEnumerable<string?> s)
                {
                    this.s = s;
                }

                void M()
                {
                    IEnumerable<string?> s;
                    new C(s);
                }
            }
            """);

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/45808")]
    public Task TestWithUnsafe_Field()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                unsafe void M(int* x)
                {
                    new [|C|](x);
                }
            }
            """,
            """
            class C
            {
                private unsafe int* x;

                public unsafe C(int* x)
                {
                    this.x = x;
                }

                unsafe void M(int* x)
                {
                    new C(x);
                }
            }
            """);

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/45808")]
    public Task TestWithUnsafe_Property()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                unsafe void M(int* x)
                {
                    new [|C|](x);
                }
            }
            """,
            """
            class C
            {
                public unsafe C(int* x)
                {
                    X = x;
                }

                public unsafe int* X { get; }

                unsafe void M(int* x)
                {
                    new C(x);
                }
            }
            """, index: 1);

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/45808")]
    public Task TestWithUnsafeInUnsafeClass_Field()
        => TestInRegularAndScriptAsync(
            """
            unsafe class C
            {
                void M(int* x)
                {
                    new [|C|](x);
                }
            }
            """,
            """
            unsafe class C
            {
                private int* x;

                public C(int* x)
                {
                    this.x = x;
                }

                void M(int* x)
                {
                    new C(x);
                }
            }
            """);

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/45808")]
    public Task TestWithUnsafeInUnsafeClass_Property()
        => TestInRegularAndScriptAsync(
"""
unsafe class C
{
    void M(int* x)
    {
        new [|C|](x);
    }
}
""",
"""
unsafe class C
{
    public C(int* x)
    {
        X = x;
    }

    public int* X { get; }

    void M(int* x)
    {
        new C(x);
    }
}
""", index: 1);

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/45808")]
    public Task TestUnsafeDelegateConstructor()
        => TestInRegularAndScriptAsync(
            """
            class A
            {
                public unsafe A(int* a) { }

                public unsafe A(int* a, int b, int c) : [|this(a, b)|] { }
            }
            """,
            """
            class A
            {
                private int b;

                public unsafe A(int* a) { }

                public unsafe A(int* a, int b) : this(a)
                {
                    this.b = b;
                }

                public unsafe A(int* a, int b, int c) : this(a, b) { }
            }
            """);

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/45808")]
    public Task TestUnsafeDelegateConstructorInUnsafeClass()
        => TestInRegularAndScriptAsync(
"""
 unsafe class A
 {
     public A(int* a) { }

     public A(int* a, int b, int c) : [|this(a, b)|] { }
 }
 """,
"""
 unsafe class A
 {
     private int b;

     public A(int* a) { }

     public A(int* a, int b) : this(a)
     {
         this.b = b;
     }

     public A(int* a, int b, int c) : this(a, b) { }
 }
 """);

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/44708")]
    public Task TestDoNotGenerateNameFromTypeArgumentIfNotEnumerable()
        => TestInRegularAndScriptAsync(
"""
 class Frog<T> { }

 class C
 {
     C M()
     {
         return new [||]C(new Frog<int>());
     }
 }
 """,
"""
 class Frog<T> { }

 class C
 {
     private Frog<int> frog;

     public C(Frog<int> frog)
     {
         this.frog = frog;
     }

     C M()
     {
         return new C(new Frog<int>());
     }
 }
 """);

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/44708")]
    public Task TestGenerateNameFromTypeArgumentForTupleType()
        => TestInRegularAndScriptAsync(
"""
 using System.Collections.Generic;

 class Frog { }

 class C
 {
     C M() => new [||]C(new List<(int, string)>());
 }
 """,
"""
 using System.Collections.Generic;

 class Frog { }

 class C
 {
     private List<(int, string)> list;

     public C(List<(int, string)> list)
     {
         this.list = list;
     }

     C M() => new C(new List<(int, string)>());
 }
 """);

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/47928")]
    public Task TestGenerateConstructorFromImplicitObjectCreation()
        => TestInRegularAndScriptAsync("""
            namespace N
            {
                public class B
                {
                    void M()
                    {
                        C c = [||]new(0);
                    }
                }

                public class C
                {
                }
            }
            """, """
            namespace N
            {
                public class B
                {
                    void M()
                    {
                        C c = new(0);
                    }
                }

                public class C
                {
                    private int v;

                    public C(int v)
                    {
                        this.v = v;
                    }
                }
            }
            """);

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/47928")]
    public Task TestGenerateConstructorFromImplicitObjectCreation_Properties()
        => TestInRegularAndScriptAsync("""
            namespace N
            {
                public class B
                {
                    void M()
                    {
                        C c = [||]new(0);
                    }
                }

                public class C
                {
                }
            }
            """, """
            namespace N
            {
                public class B
                {
                    void M()
                    {
                        C c = new(0);
                    }
                }

                public class C
                {
                    public C(int v)
                    {
                        V = v;
                    }

                    public int V { get; }
                }
            }
            """, index: 1);

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/47928")]
    public Task TestGenerateConstructorFromImplicitObjectCreation_NoField()
        => TestInRegularAndScriptAsync("""
            namespace N
            {
                public class B
                {
                    void M()
                    {
                        C c = [||]new(0);
                    }
                }

                public class C
                {
                }
            }
            """, """
            namespace N
            {
                public class B
                {
                    void M()
                    {
                        C c = new(0);
                    }
                }

                public class C
                {
                    public C(int v)
                    {
                    }
                }
            }
            """, index: 2);

    [Fact]
    public Task TestGenerateConstructorFromImplicitObjectCreation_Delegating()
        => TestInRegularAndScriptAsync(
            """
            class C
            {
                void M()
                {
                    D d = [||]new(1);
                }
            }

            class B
            {
                protected B(int x)
                {
                }
            }

            class D : B
            {
            }
            """,
            """
            class C
            {
                void M()
                {
                    D d = new(1);
                }
            }

            class B
            {
                protected B(int x)
                {
                }
            }

            class D : B
            {
                public D(int x) : base(x)
                {
                }
            }
            """);

    [Fact]
    public async Task TestGenerateConstructorFromImplicitObjectCreation_DelegatingFromParameter()
    {
        const string input =
            """
            class C
            {
                void M(D d)
                {
                    M([||]new(1));
                }
            }

            class B
            {
                protected B(int x)
                {
                }
            }

            class D : B
            {
            }
            """;

        await TestActionCountAsync(input, 1);
        await TestInRegularAndScriptAsync(
     input,
     """
     class C
     {
         void M(D d)
         {
             M(new(1));
         }
     }

     class B
     {
         protected B(int x)
         {
         }
     }

     class D : B
     {
         public D(int x) : base(x)
         {
         }
     }
     """);
    }

    [Fact]
    public Task TestDelegateWithLambda1()
        => TestInRegularAndScriptAsync(
            """
            using System;

            class A
            {
                void M()
                {
                    Delta d1 = new [|Delta|](x => x.Length, 3);
                }
            }

            class Delta
            {
                public Delta(Func<string, int> f)
                {
                }
            }
            """,
            """
            using System;

            class A
            {
                void M()
                {
                    Delta d1 = new Delta(x => x.Length, 3);
                }
            }

            class Delta
            {
                private int v;

                public Delta(Func<string, int> f)
                {
                }

                public Delta(Func<string, int> f, int v) : this(f)
                {
                    this.v = v;
                }
            }
            """);

    [Fact]
    public Task TestDelegateWithLambda2()
        => TestInRegularAndScriptAsync(
            """
            using System;

            class A
            {
                public A(Func<string, int> f) { }

                void M()
                {
                    Delta d1 = new [|Delta|](x => x.Length, 3);
                }
            }

            class Delta : A
            {
            }
            """,
            """
            using System;

            class A
            {
                public A(Func<string, int> f) { }

                void M()
                {
                    Delta d1 = new Delta(x => x.Length, 3);
                }
            }

            class Delta : A
            {
                private int v;

                public Delta(Func<string, int> f, int v) : base(f)
                {
                    this.v = v;
                }
            }
            """);

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/50765")]
    public Task TestDelegateConstructorWithMissingType()
        => TestAsync("""
            <Workspace>
                <Project Language="C#" Name="CSharpProjectWithExtraType" CommonReferences="true">
                    <Document>
            public class ExtraType { }
                    </Document>
                </Project>
                <Project Language="C#" Name="CSharpProjectGeneratingInto" CommonReferences="true">
                    <ProjectReference>CSharpProjectWithExtraType</ProjectReference>
                    <Document>
            public class C
            {
                public C(ExtraType t) { }
                public C(string s, int i) { }
            }</Document>
                </Project>
                <Project Language="C#" CommonReferences="true">
                    <ProjectReference>CSharpProjectGeneratingInto</ProjectReference>
                    <Document>
            public class InvokingConstructor
            {
                public void M()
                {
                    [|new C(42, 42)|];
                }
            }</Document>
                </Project>
            </Workspace>
            """,
            """

            public class C
            {
                private int v1;
                private int v2;

                public C(ExtraType t) { }
                public C(string s, int i) { }

                public C(int v1, int v2)
                {
                    this.v1 = v1;
                    this.v2 = v2;
                }
            }
            """, new(parseOptions: TestOptions.Regular));

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/38822")]
    public Task TestMissingInLambdaWithCallToExistingConstructor()
        => TestMissingInRegularAndScriptAsync(
            """
            using System;

            public class InstanceType
            {
                public InstanceType(object? a = null) { }
            }

            public static class Example
            {
                public static void Test()
                {
                    Action lambda = () =>
                    {
                        var _ = new [|InstanceType|]();
                        var _ = 0
                    };
                }
            }
            """);

#if !CODE_STYLE

    [Fact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/530003")]
    public Task TestAttributesWithAllValidParameters()
        => TestInRegularAndScriptAsync(
            """
            using System;

            enum A
            {
                A1
            }

            [AttributeUsage(AttributeTargets.Class)]
            class MyAttrAttribute : Attribute
            {
            }

            [|[MyAttrAttribute(new int[] { 1, 2, 3 }, A.A1, true, (byte)1, 'a', (short)12, (int)1, (long)5L, 5D, 3.5F, "hello")]|]
            class D
            {
            }
            """,
            """
            using System;

            enum A
            {
                A1
            }

            [AttributeUsage(AttributeTargets.Class)]
            class MyAttrAttribute : Attribute
            {
                private int[] ints;
                private A a1;
                private bool v1;
                private byte v2;
                private char v3;
                private short v4;
                private int v5;
                private long v6;
                private double v7;
                private float v8;
                private string v9;

                public MyAttrAttribute(int[] ints, A a1, bool v1, byte v2, char v3, short v4, int v5, long v6, double v7, float v8, string v9)
                {
                    this.ints = ints;
                    this.a1 = a1;
                    this.v1 = v1;
                    this.v2 = v2;
                    this.v3 = v3;
                    this.v4 = v4;
                    this.v5 = v5;
                    this.v6 = v6;
                    this.v7 = v7;
                    this.v8 = v8;
                    this.v9 = v9;
                }
            }

            [MyAttrAttribute(new int[] { 1, 2, 3 }, A.A1, true, (byte)1, 'a', (short)12, (int)1, (long)5L, 5D, 3.5F, "hello")]
            class D
            {
            }
            """);

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/44708")]
    public Task TestGenerateNameFromTypeArgument()
        => TestInRegularAndScriptAsync(
"""
 using System.Collections.Generic;

 class Frog { }

 class C
 {
     C M() => new [||]C(new List<Frog>());
 }
 """,
"""
 using System.Collections.Generic;

 class Frog { }

 class C
 {
     private List<Frog> frogs;

     public C(List<Frog> frogs)
     {
         this.frogs = frogs;
     }

     C M() => new C(new List<Frog>());
 }
 """);

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/44708")]
    public Task TestGenerateNameFromTypeArgumentForErrorType()
        => TestInRegularAndScriptAsync(
"""
 using System.Collections.Generic;

 class Frog { }

 class C
 {
     C M() => new [||]C(new List<>());
 }
 """,
"""
 using System.Collections.Generic;

 class Frog { }

 class C
 {
     private List<T> ts;

     public C(List<T> ts)
     {
         this.ts = ts;
     }

     C M() => new C(new List<>());
 }
 """);

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/44708")]
    public Task TestGenerateNameFromTypeArgumentInNamespace()
        => TestInRegularAndScriptAsync(
"""
 using System.Collections.Generic;

 namespace N {
     class Frog { }

     class C
     {
         C M() => new [||]C(new List<Frog>());
     }
 }
 """,
"""
 using System.Collections.Generic;

 namespace N {
     class Frog { }

     class C
     {
         private List<Frog> frogs;

         public C(List<Frog> frogs)
         {
             this.frogs = frogs;
         }

         C M() => new C(new List<Frog>());
     }
 }
 """);

#endif
}
