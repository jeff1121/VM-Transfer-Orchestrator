namespace VMTO.API.Auth;

// 角色定義常數
public static class Roles
{
    public const string Admin = "Admin";
    public const string Operator = "Operator";
    public const string Viewer = "Viewer";
    // 用於 RequireAuthorization 的策略組合
    public const string AdminOrOperator = "Admin,Operator";
    public const string All = "Admin,Operator,Viewer";
}
