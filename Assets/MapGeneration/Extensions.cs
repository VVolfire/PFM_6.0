using Scellecs.Morpeh;

public static class EntityExtentions
{

    public static bool IsExist(this Entity entity)
    {
        if (entity.Id == 0) { return false; }
        return true;
    }
}
