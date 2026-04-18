using Dapper;
using ECommerce.Models;
using System.Data;
using Npgsql;

namespace ECommerce.Data;

public static class DapperTypeHandlers
{
    public static void RegisterTypeHandlers()
    {
        SqlMapper.AddTypeHandler(new UserRoleTypeHandler());
        SqlMapper.AddTypeHandler(new OrderStatusTypeHandler());
        SqlMapper.AddTypeHandler(new DiscountTypeTypeHandler());
        SqlMapper.AddTypeHandler(new PaymentStatusTypeHandler());
        SqlMapper.AddTypeHandler(new StringArrayTypeHandler());
        SqlMapper.AddTypeHandler(new IntArrayTypeHandler());
        SqlMapper.AddTypeHandler(new IntArrayTypeHandler());
    }
}

public class UserRoleTypeHandler : SqlMapper.TypeHandler<UserRole>
{
    public override void SetValue(IDbDataParameter parameter, UserRole value)
    {
        parameter.Value = value.ToString();
    }

    public override UserRole Parse(object value)
    {
        return Enum.Parse<UserRole>(value.ToString() ?? "Customer");
    }
}

public class OrderStatusTypeHandler : SqlMapper.TypeHandler<OrderStatus>
{
    public override void SetValue(IDbDataParameter parameter, OrderStatus value)
    {
        parameter.Value = value.ToString();
    }

    public override OrderStatus Parse(object value)
    {
        if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
        {
            return OrderStatus.Pending;
        }
        
        var statusString = value.ToString()!;
        
        // Handle case-insensitive parsing and map database values to enum
        if (Enum.TryParse<OrderStatus>(statusString, ignoreCase: true, out var status))
        {
            return status;
        }
        
        // Fallback to Pending if parsing fails
        return OrderStatus.Pending;
    }
}

public class DiscountTypeTypeHandler : SqlMapper.TypeHandler<DiscountType>
{
    public override void SetValue(IDbDataParameter parameter, DiscountType value)
    {
        parameter.Value = value.ToString();
    }

    public override DiscountType Parse(object value)
    {
        return Enum.Parse<DiscountType>(value.ToString() ?? "Percentage");
    }
}

public class PaymentStatusTypeHandler : SqlMapper.TypeHandler<PaymentStatus>
{
    public override void SetValue(IDbDataParameter parameter, PaymentStatus value)
    {
        parameter.Value = value.ToString();
    }

    public override PaymentStatus Parse(object value)
    {
        return Enum.Parse<PaymentStatus>(value.ToString() ?? "Pending");
    }
}

public class StringArrayTypeHandler : SqlMapper.TypeHandler<List<string>>
{
    public override void SetValue(IDbDataParameter parameter, List<string> value)
    {
        if (parameter is NpgsqlParameter npgsqlParameter)
        {
            npgsqlParameter.Value = value?.ToArray() ?? Array.Empty<string>();
        }
        else
        {
            parameter.Value = value;
        }
    }

    public override List<string> Parse(object value)
    {
        if (value == null || value == DBNull.Value)
            return new List<string>();

        if (value is string[] stringArray)
            return stringArray.ToList();

        if (value is string str)
            return new List<string> { str };

        return new List<string>();
    }
}

public class IntArrayTypeHandler : SqlMapper.TypeHandler<List<int>>
{
    public override void SetValue(IDbDataParameter parameter, List<int> value)
    {
        if (parameter is NpgsqlParameter npgsqlParameter)
        {
            npgsqlParameter.Value = value?.ToArray() ?? Array.Empty<int>();
        }
        else
        {
            parameter.Value = value;
        }
    }

    public override List<int> Parse(object value)
    {
        if (value == null || value == DBNull.Value)
            return new List<int>();

        if (value is int[] intArray)
            return intArray.ToList();

        if (value is object[] objArray)
            return objArray.Select(o => Convert.ToInt32(o)).ToList();

        return new List<int>();
    }
}

