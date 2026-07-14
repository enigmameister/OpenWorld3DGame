using System;

[Flags]
public enum AccessPermission
{
    None = 0,

    Visitor = 1 << 0,
    Staff = 1 << 1,
    Mission = 1 << 2,
    LawEnforcement = 1 << 3
}