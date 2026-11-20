using WixToolset.Dtf.WindowsInstaller;

public static class MatrixActions
{
    [CustomAction]
    public static ActionResult CancelRemoval(Session session) => ActionResult.UserExit;
    [CustomAction]
    public static ActionResult FailRemoval(Session session) => ActionResult.Failure;
}
