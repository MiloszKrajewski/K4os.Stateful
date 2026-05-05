using K4os.Stateful.Internal;

namespace K4os.Stateful.Tests;

public class TypeExtensionsTests
{
    // Primary hierarchy: C : B : A : object
    //   A introduces IBase (d=2), B introduces IMid (d=1), C introduces ILeaf (d=0)
    private interface IBase;
    private interface IMid;
    private interface ILeaf;
    private class A: IBase;
    private class B: A, IMid;
    private class C: B, ILeaf;

    // Multiple interfaces introduced at the same class
    private interface IAlpha;
    private interface IBeta;
    private class D: IAlpha, IBeta;

    // Interface-on-interface: both IMiddle and IGrandparent introduced at Gamma
    private interface IGrandparent;
    private interface IMiddle: IGrandparent;
    private class Gamma: IMiddle;

    // Base interface already present via base class:
    //   BaseClass introduces IRoot (d=1); DerivedClass introduces IBranch (d=0)
    //   IRoot is NOT re-introduced at DerivedClass because BaseClass already has it
    private interface IRoot;
    private interface IBranch: IRoot;
    private class BaseClass: IRoot;
    private class DerivedClass: BaseClass, IBranch;

    // Unrelated and sibling types
    private class Unrelated;
    private interface IShared;
    private class Sibling1: IShared;
    private class Sibling2: IShared;

    [Fact]
    public void DistanceFrom_SameType_ReturnsZero() =>
        Assert.Equal(0, typeof(C).DistanceFrom(typeof(C)));

    [Fact]
    public void DistanceFrom_DirectBaseClass_ReturnsOne() =>
        Assert.Equal(1, typeof(C).DistanceFrom(typeof(B)));

    [Fact]
    public void DistanceFrom_GrandparentClass_ReturnsTwo() =>
        Assert.Equal(2, typeof(C).DistanceFrom(typeof(A)));

    [Fact]
    public void DistanceFrom_InterfaceOnActualType_ReturnsZero() =>
        Assert.Equal(0, typeof(C).DistanceFrom(typeof(ILeaf)));

    [Fact]
    public void DistanceFrom_InterfaceOnDirectBase_ReturnsOne() =>
        Assert.Equal(1, typeof(C).DistanceFrom(typeof(IMid)));

    [Fact]
    public void DistanceFrom_InterfaceOnGrandparent_ReturnsTwo() =>
        Assert.Equal(2, typeof(C).DistanceFrom(typeof(IBase)));

    [Fact]
    public void DistanceFrom_Object_ReturnsThree() =>
        Assert.Equal(3, typeof(C).DistanceFrom(typeof(object)));

    [Fact]
    public void DistanceFrom_InterfaceIsStrictlyLessThanObject()
    {
        var ibaseDist = typeof(C).DistanceFrom(typeof(IBase));
        var objectDist = typeof(C).DistanceFrom(typeof(object));
        Assert.True(ibaseDist < objectDist);
    }

    [Fact]
    public void DistanceFrom_MultipleInterfacesAtSameClass_BothReturnZero()
    {
        Assert.Equal(0, typeof(D).DistanceFrom(typeof(IAlpha)));
        Assert.Equal(0, typeof(D).DistanceFrom(typeof(IBeta)));
    }

    [Fact]
    public void DistanceFrom_InterfaceOnInterface_BothReturnZeroWhenIntroducedTogether()
    {
        Assert.Equal(0, typeof(Gamma).DistanceFrom(typeof(IMiddle)));
        Assert.Equal(0, typeof(Gamma).DistanceFrom(typeof(IGrandparent)));
    }

    [Fact]
    public void DistanceFrom_BaseInterfaceRetainedAtBaseClassDistance()
    {
        Assert.Equal(0, typeof(DerivedClass).DistanceFrom(typeof(IBranch)));
        Assert.Equal(1, typeof(DerivedClass).DistanceFrom(typeof(IRoot)));
    }

    [Fact]
    public void DistanceFrom_InterfaceChild_ReturnsNull() =>
        Assert.Null(typeof(ILeaf).DistanceFrom(typeof(object)));

    [Fact]
    public void DistanceFrom_InterfaceToInterface_Throws() =>
        Assert.Throws<InvalidOperationException>(() => typeof(IMiddle).DistanceFrom(typeof(IGrandparent)));

    [Fact]
    public void DistanceFrom_UnrelatedClass_ReturnsNull() =>
        Assert.Null(typeof(C).DistanceFrom(typeof(Unrelated)));

    [Fact]
    public void DistanceFrom_SiblingClass_ReturnsNull() =>
        Assert.Null(typeof(Sibling1).DistanceFrom(typeof(Sibling2)));

    [Fact]
    public void DistanceFrom_CalledTwice_ReturnsSameValue()
    {
        var first = typeof(C).DistanceFrom(typeof(A));
        var second = typeof(C).DistanceFrom(typeof(A));
        Assert.Equal(first, second);
    }
}
