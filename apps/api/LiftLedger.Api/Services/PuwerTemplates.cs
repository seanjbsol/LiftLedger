using LiftLedger.Api.Contracts;
using LiftLedger.Api.Domain;

namespace LiftLedger.Api.Services;

public static class PuwerTemplates
{
    public static IReadOnlyList<PuwerTemplateDto> All { get; } =
    [
        Template(
            "puwer-trailer-v1",
            AssetCategory.Trailer,
            "PUWER assessment — trailer",
            "Practical workplace assessment notes for trailers used at work. This is a LiftLedger working document, not an HSE-certified form.",
            [
                ("structure", "Structure", "Chassis, bed, headboard and bodywork free from serious distortion, cracks or corrosion that could affect strength."),
                ("coupling", "Coupling", "Coupling, hitch, eye and safety chain suitable and in serviceable condition."),
                ("brakes", "Brakes", "Service and parking brakes operate effectively; breakaway cable present where required."),
                ("running-gear", "Running gear", "Tyres, wheels, mudguards and suspension fit for the intended load and speed."),
                ("lighting", "Lighting", "Lights, reflectors and number plate lamp work and are visible."),
                ("restraints", "Load restraint", "Lashing points, ramps and winches suitable for the loads to be carried."),
                ("markings", "Information", "Identification, plated weights and any operating instructions are legible."),
                ("environment", "Use", "Intended use, towing vehicle match and site conditions considered.")
            ]),
        Template(
            "puwer-plant-v1",
            AssetCategory.Plant,
            "PUWER assessment — plant",
            "Practical workplace assessment notes for powered plant. This is a LiftLedger working document, not an HSE-certified form.",
            [
                ("identity", "Identity", "Machine identity, serial number and any rated capacity are marked and legible."),
                ("guards", "Guards", "Dangerous parts are guarded; guards are in place and interlocks function where fitted."),
                ("controls", "Controls", "Controls are marked, function as intended and include an emergency stop that is reachable."),
                ("hydraulics", "Power systems", "Hydraulics, electrics and fuel systems show no dangerous leaks, damage or exposed conductors."),
                ("stability", "Stability", "Outriggers, counterweight and tyres/tracks suitable for the intended work."),
                ("visibility", "Operator", "Visibility, seat, restraint and access are suitable; operator instructions available."),
                ("maintenance", "Maintenance", "Maintenance access is safe; isolation and residual energy considered."),
                ("environment", "Workplace", "Intended environment (site, workshop, highway) and other persons at work considered.")
            ]),
        Template(
            "puwer-lifting-equipment-v1",
            AssetCategory.LiftingEquipment,
            "PUWER assessment — lifting equipment",
            "Workplace assessment notes for lifting equipment used at work, in addition to any LOLER thorough examination. Not an HSE-certified form.",
            [
                ("suitability", "Suitability", "Equipment is suitable for the loads, configuration and location of use."),
                ("installation", "Installation", "Installed, anchored or supported so that it remains stable in use."),
                ("controls", "Controls", "Controls, limit devices and emergency lowering/stop (where fitted) function."),
                ("access", "Access", "Safe access for operation, maintenance and slinging."),
                ("markings", "Markings", "SWL, configuration chart and identification are legible."),
                ("environment", "Environment", "Overhead obstructions, wind, ground and other persons at work considered."),
                ("maintenance", "Maintenance", "Isolation, residual energy and inspection access are practical."),
                ("information", "Information", "Operator instructions and any restrictions on use are available.")
            ]),
        Template(
            "puwer-lifting-accessory-v1",
            AssetCategory.LiftingAccessory,
            "PUWER assessment — lifting accessory",
            "Workplace assessment notes for slings, shackles and other accessories. Not an HSE-certified form.",
            [
                ("identity", "Identity", "Unique identification and SWL are marked and match the register."),
                ("condition", "Condition", "No dangerous wear, stretch, distortion, cracked fittings or damaged stitching."),
                ("compatibility", "Compatibility", "Accessory is compatible with the load, hitch and host equipment."),
                ("storage", "Storage", "Stored so that it will not be damaged and remains identifiable."),
                ("use", "Use", "Intended hitch, angle and environment considered."),
                ("withdrawal", "Withdrawal", "Criteria for taking out of service are understood by users.")
            ]),
        Template(
            "puwer-other-v1",
            AssetCategory.Other,
            "PUWER assessment — other work equipment",
            "Generic workplace assessment notes for other work equipment. Not an HSE-certified form.",
            [
                ("suitability", "Suitability", "Equipment is suitable for the work and persons who will use it."),
                ("condition", "Condition", "No dangerous defects, missing parts or improvised repairs."),
                ("guards", "Guards and controls", "Guards, controls and stop devices (where relevant) are effective."),
                ("information", "Information", "Instructions, markings and any restrictions on use are available."),
                ("maintenance", "Maintenance", "It can be maintained and inspected safely."),
                ("environment", "Workplace", "Location, other persons at work and environment considered.")
            ])
    ];

    public static PuwerTemplateDto ForCategory(AssetCategory category) =>
        All.First(t => t.Category == category);

    public static PuwerTemplateDto? Find(string? code) =>
        string.IsNullOrWhiteSpace(code)
            ? null
            : All.FirstOrDefault(t => t.Code.Equals(code.Trim(), StringComparison.OrdinalIgnoreCase));

    private static PuwerTemplateDto Template(
        string code,
        AssetCategory category,
        string title,
        string introduction,
        (string Id, string Section, string Prompt)[] items) =>
        new(
            code,
            category,
            title,
            introduction,
            items.Select(i => new PuwerTemplateItemDto(i.Id, i.Section, i.Prompt)).ToList());
}
