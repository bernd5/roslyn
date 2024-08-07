﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.VisualStudio.Debugger.Evaluation.ClrCompilation;
using Microsoft.VisualStudio.Debugger.Metadata;
using Roslyn.Utilities;
using Type = Microsoft.VisualStudio.Debugger.Metadata.Type;

namespace Microsoft.CodeAnalysis.ExpressionEvaluator
{
    [Flags]
    internal enum DeclarationInfo : byte
    {
        /// <summary>
        /// A declaration with this name has not been encountered.
        /// </summary>
        None = 0,
        /// <summary>
        /// This member is defined on the declared type or one of its base classes.
        /// </summary>
        FromDeclaredTypeOrBase = 0,
        /// <summary>
        /// This member is defined on a type that inherits from the declared type (is more derived).
        /// </summary>
        FromSubTypeOfDeclaredType = 1,
        /// <summary>
        /// This member should be hidden (under "Non-Public members" node), because Just My Code is on and
        /// no symbols have been loaded for the declaring type's module.
        /// </summary>
        HideNonPublic = 1 << 2,
        /// <summary>
        /// More than one non-virtual member with this name exists in the type hierarchy.
        /// The ResultProvider should include the declaring type of this member in the member name to disambiguate.
        /// </summary>
        IncludeTypeInMemberName = 1 << 3,
        /// <summary>
        /// The full name for this member access expression will require a cast to the declaring type.
        /// </summary>
        RequiresExplicitCast = 1 << 4,
    }

    internal static class DeclarationInfoExtensions
    {
        internal static bool IsSet(this DeclarationInfo info, DeclarationInfo value)
        {
            return (info & value) == value;
        }
    }

    internal readonly struct MemberAndDeclarationInfo
    {
        public static readonly IComparer<MemberAndDeclarationInfo> Comparer = new MemberNameComparer();

        private readonly MemberInfo _member;

        public readonly string DisplayName;
        public readonly DkmClrDebuggerBrowsableAttributeState? BrowsableState;
        public readonly bool IsGenerated;

        public readonly bool CanFavorite;
        public readonly bool IsFavorite;

        /// <summary>
        /// Exists to correctly order fields with the same name from different types in the inheritance hierarchy.
        /// </summary>
        private readonly int _inheritanceLevel;

        private readonly DeclarationInfo _info;

        public MemberAndDeclarationInfo(MemberInfo member, string displayName, DkmClrDebuggerBrowsableAttributeState? browsableState, DeclarationInfo info, int inheritanceLevel, bool isGenerated, bool canFavorite, bool isFavorite)
        {
            Debug.Assert(member != null);

            _member = member;
            _info = info;
            _inheritanceLevel = inheritanceLevel;

            DisplayName = displayName;
            BrowsableState = browsableState;
            CanFavorite = canFavorite && SupportsCanFavorite(member, info);
            IsFavorite = isFavorite;
            IsGenerated = isGenerated;
        }

        public bool HideNonPublic => _info.IsSet(DeclarationInfo.HideNonPublic);
        public bool IncludeTypeInMemberName => _info.IsSet(DeclarationInfo.IncludeTypeInMemberName);
        public bool RequiresExplicitCast => _info.IsSet(DeclarationInfo.RequiresExplicitCast);

        public Type DeclaringType
        {
            get
            {
                return _member.DeclaringType;
            }
        }

        public bool IsPublic
        {
            get
            {
                return _member.IsPublic();
            }
        }

        public bool IsStatic
        {
            get
            {
                return IsMemberStatic(_member);
            }
        }

        public MemberTypes MemberType
        {
            get
            {
                return _member.MemberType;
            }
        }

        public string MetadataName
        {
            get
            {
                return _member.Name;
            }
        }

        public Type Type
        {
            get
            {
                return GetMemberType(_member);
            }
        }

        public Type OriginalDefinitionType
        {
            get
            {
                return GetMemberType(_member.GetOriginalDefinition());
            }
        }

        private static Type GetMemberType(MemberInfo member)
        {
            switch (member.MemberType)
            {
                case MemberTypes.Field:
                    return ((FieldInfo)member).FieldType;
                case MemberTypes.Property:
                    return ((PropertyInfo)member).PropertyType;
                default:
                    throw ExceptionUtilities.UnexpectedValue(member.MemberType);
            }
        }

        private static bool SupportsCanFavorite(MemberInfo member, DeclarationInfo info)
        {
            if (IsMemberStatic(member))
            {
                return false;
            }

            Type memberType = GetMemberType(member);

            if (memberType.IsByRef || memberType.IsPointer)
            {
                return false;
            }

            if (member.Name.Contains("."))
            {
                return false;
            }

            if (info.IsSet(DeclarationInfo.IncludeTypeInMemberName))
            {
                return false;
            }

            return true;
        }

        private static bool IsMemberStatic(MemberInfo member)
        {
            switch (member.MemberType)
            {
                case MemberTypes.Field:
                    return ((FieldInfo)member).IsStatic;
                case MemberTypes.Property:
                    return ((PropertyInfo)member).GetGetMethod(nonPublic: true).IsStatic;
                default:
                    throw ExceptionUtilities.UnexpectedValue(member.MemberType);
            }
        }

        public DkmClrCustomTypeInfo TypeInfo
        {
            get
            {
                switch (_member.MemberType)
                {
                    case MemberTypes.Field:
                    case MemberTypes.Property:
                        return _member.GetCustomAttributesData().GetCustomTypeInfo();
                    default:
                        // If we ever see a method, we'll have to use ReturnTypeCustomAttributes.
                        throw ExceptionUtilities.UnexpectedValue(_member.MemberType);
                }
            }
        }

#nullable enable
        public bool TryGetExplicitlyImplementedInterface([NotNullWhen(true)] out Type? interfaceType, [NotNullWhen(true)] out string? memberDisplayName)
        {
            // We only display fields and properties and fields never implement interface members.
            if (_member.MemberType == MemberTypes.Property)
            {
                // A dot is neither necessary nor sufficient for determining whether a member explicitly
                // implements an interface member, but it does characterize the set of members we're
                // interested in displaying differently.  For example, if the property is from VB, it will
                // be an explicit interface implementation, but will not have a dot.
                var dotPos = _member.Name.LastIndexOf('.');
                if (dotPos >= 0)
                {
                    var property = (PropertyInfo)_member;
                    var accessors = property.GetAccessors(nonPublic: true);
                    Debug.Assert(accessors.Length > 0);

                    // We'll just pick the first interface we find since we don't have a good way
                    // to display more than one.
                    foreach (var accessor in accessors)
                    {
                        foreach (var interfaceAccessor in accessor.GetExplicitInterfacesImplemented())
                        {
                            memberDisplayName = _member.Name.Substring(dotPos + 1);
                            interfaceType = interfaceAccessor.DeclaringType;
                            RoslynDebug.AssertNotNull(interfaceType);
                            return true;
                        }
                    }
                }
            }

            interfaceType = null;
            memberDisplayName = null;
            return false;
        }

        private sealed class MemberNameComparer : IComparer<MemberAndDeclarationInfo>
        {
            public int Compare(MemberAndDeclarationInfo x, MemberAndDeclarationInfo y)
            {
                var comp = string.Compare(x.DisplayName, y.DisplayName, StringComparison.Ordinal);
                return comp != 0 ? comp : (y._inheritanceLevel - x._inheritanceLevel);
            }
        }
    }
}
