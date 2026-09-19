namespace LiftLedger.Api.Domain;

public enum MembershipRole
{
    Owner = 0,
    Admin = 1,
    Examiner = 2,
    Viewer = 3
}

public enum AssetCategory
{
    Trailer = 0,
    Plant = 1,
    LiftingEquipment = 2,
    LiftingAccessory = 3,
    Other = 4
}

public enum ExaminationType
{
    ThoroughExamination = 0,
    InterimInspection = 1,
    PreUseCheck = 2,
    PuwerInspection = 3
}

public enum InspectionResult
{
    Pass = 0,
    PassWithDefects = 1,
    Fail = 2
}

public enum InspectionStatus
{
    Draft = 0,
    Completed = 1
}

public enum DefectSeverity
{
    Observation = 0,
    Defect = 1,
    ImmediateDanger = 2
}
