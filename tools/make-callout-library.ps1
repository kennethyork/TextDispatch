# Writes the callout library: one XML recipe per callout, from the table below.
#
#   powershell -ExecutionPolicy Bypass -File tools\make-callout-library.ps1
#
# They ship with the pack and are read from Plugins\LSPDFR\TextCallouts\Library, which is a different
# folder from the player's own Custom one: the library belongs to the pack and is replaced when the pack
# is updated, Custom belongs to the player and is never touched.
#
# The <For> element decides which duty a callout is offered to. Values that mean something:
#
#   police      any police agency (the default)          ems        the medical branch
#   lspd        Los Santos Police                        fire       the fire branch
#   sheriff     Los Santos County Sheriff (lssd)         any        anybody
#   sahp        San Andreas Highway Patrol               ranger     San Andreas Park Rangers
#   nysp        North Yankton State Patrol               prison     Bolingbroke (saspa)
#   fib  iaa  noose  swat
#
# Every callout's script - what its people say when the player talks to them - lives in
# tools\callout-dialogue.psd1, keyed by id, because it is the one part of a recipe that is writing
# rather than wiring. Add attaches it to the entry as it is registered, so the table below stays a table
# of scenes, and the checks before the write refuse to build a library where any callout has no script.
#
# Every access below is by index - $r['count'], never $r.count - because a hashtable's own properties
# (Count, Keys, Values, Item) are not keys, and $r.count quietly returns the number of keys instead.

param(
    [string] $OutputDirectory = ""
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repo 'src\TextCallouts\Library' }
if (Test-Path $OutputDirectory) { Remove-Item $OutputDirectory -Recurse -Force }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

$library = @()

$dialoguePath = Join-Path $PSScriptRoot 'callout-dialogue.psd1'
if (-not (Test-Path $dialoguePath)) { throw "the dialogue file is missing: $dialoguePath" }
$dialogue = Import-PowerShellDataFile -Path $dialoguePath

# An entry may still carry its own talk= and that wins, but nothing should need to: the file is meant to
# be the whole of what is said, in one place, so that "every callout has a script" is one thing to read.
function Add($entry) {
    if (-not $entry.ContainsKey('talk')) {
        $id = [string]$entry['id']
        if ($dialogue.ContainsKey($id)) { $entry['talk'] = $dialogue[$id] }
    }
    $script:library += $entry
}

# ============================================================ Who is in the scene
#
# Every recipe's people are drawn at random from a pool, so the suspect in an armed robbery is rarely the
# same suspect twice, and three suspects in one street are three different men. Three things about them:
#
#   * They are men, because the dispatch text these recipes carry says "they" - 103 of the 326 do, and a
#     random woman in a scene dispatch is calling "they" is a worse look than a familiar face. Varying the
#     sex as well means rewording that prose first, which is a pass of its own.
#   * The pack checks each candidate against the game before it spawns it and falls back if none exist,
#     so a pool with a typo in it cannot leave a callout with nobody in it - a scene with nobody in it
#     closes the moment it starts.
#   * A pool is a theme, not a costume list: city work draws on the downtown crowd, county work on the
#     farms and the hills, the parks on people dressed for the outdoors. Nothing here should read as a
#     joke at the scene's expense.
#
# The first name in the city pool is the fallback the pack uses when nothing else exists, so it stays
# first.

$peopleCity = @(
    'a_m_y_business_01', 'a_m_y_business_02', 'a_m_m_business_01', 'a_m_y_downtown_01',
    'a_m_y_downtown_02', 'a_m_y_genstreet_01', 'a_m_y_genstreet_02', 'a_m_y_hipster_01',
    'a_m_y_hipster_02', 'a_m_y_vinewood_01', 'a_m_y_bevhills_01', 'a_m_m_mlcrisis_01'
)

$peopleRough = @(
    'a_m_y_genstreet_01', 'a_m_y_genstreet_02', 'a_m_y_methhead_01', 'a_m_y_eastsa_01',
    'a_m_y_eastsa_02', 'a_m_m_eastsa_01', 'a_m_y_soucent_01', 'a_m_y_soucent_02',
    'a_m_m_soucent_01', 'a_m_y_stlat_01', 'a_m_y_stwhi_01', 'a_m_m_genfat_01', 'a_m_m_genfat_02'
)

$peopleCounty = @(
    'a_m_m_farmer_01', 'a_m_m_hillbilly_01', 'a_m_m_hillbilly_02', 'a_m_y_salton_01',
    'a_m_m_salton_01', 'a_m_m_salton_02', 'a_m_m_salton_03', 'a_m_m_salton_04'
)

$peopleOutdoors = @(
    'a_m_y_hipster_01', 'a_m_y_hipster_02', 'a_m_y_beach_01', 'a_m_y_beach_02',
    'a_m_y_runner_01', 'a_m_y_runner_02', 'a_m_m_farmer_01', 'a_m_y_sunbathe_01'
)

$peopleBeach = @(
    'a_m_y_beach_01', 'a_m_y_beach_02', 'a_m_y_beach_03', 'a_m_y_sunbathe_01',
    'a_m_y_musclbeac_01', 'a_m_y_musclbeac_02', 'a_m_y_jetski_01', 'a_m_y_runner_01'
)

$peopleWork = @(
    'a_m_y_construct_01', 'a_m_y_construct_02', 'a_m_m_mlcrisis_01', 'a_m_y_downtown_01',
    'a_m_m_genfat_01', 'a_m_y_business_01'
)

# The women. Same idea, same themes, and they are in every pool by default - a callout is a person and
# half of them are women. Each name is checked against the game before it is spawned and skipped if the
# game does not have it, so a name that is slightly off costs a face and nothing else.

$womenCity = @(
    'a_f_y_business_01', 'a_f_y_business_02', 'a_f_m_business_02', 'a_f_y_hipster_01',
    'a_f_y_vinewood_01', 'a_f_y_bevhills_01', 'a_f_m_bevhills_01', 'a_f_y_tourist_01'
)

$womenRough = @(
    'a_f_y_eastsa_01', 'a_f_y_eastsa_02', 'a_f_m_eastsa_01', 'a_f_y_soucent_01',
    'a_f_y_soucent_02', 'a_f_m_soucent_01', 'a_f_y_hipster_02'
)

$womenCounty = @(
    'a_f_y_hipster_02', 'a_f_y_hipster_01', 'a_f_m_fatwhite_01', 'a_f_y_soucent_01',
    'a_f_m_bodybuild_01'
)

$womenOutdoors = @(
    'a_f_y_hipster_01', 'a_f_y_hipster_02', 'a_f_y_beach_01', 'a_f_y_fitness_01',
    'a_f_m_beach_01', 'a_f_y_tourist_01'
)

$womenBeach = @(
    'a_f_y_beach_01', 'a_f_m_beach_01', 'a_f_y_fitness_01', 'a_f_y_fitness_02',
    'a_f_y_tourist_01', 'a_f_y_hipster_02'
)

$womenWork = @(
    'a_f_m_bodybuild_01', 'a_f_y_fitness_01', 'a_f_y_business_01', 'a_f_m_business_02'
)

# Vehicles, the same idea and the same safety net: a pool where the vehicle is scenery, a named model
# where the vehicle is the callout. A stolen bus is a bus, and a lorry with a dangerous load on it is a
# lorry; a car that fails to stop is any car that a person might own.
$cars = @('sultan', 'buffalo', 'premier', 'asea', 'dilettante', 'blista', 'asterope', 'primo')
$bikes = @('akuma', 'bati', 'sanchez', 'ruffian', 'pcj', 'faggio')
$vans = @('burrito', 'rumpo', 'youga', 'minivan')

# The vehicles that are the callout rather than the scenery. Everything else with a vehicle gets a pool.
$onlyVehicle = @('bus', 'ambulance', 'flatbed', 'phantom', 'trailers', 'bobcatxl', 'blazer', 'rebel')

function Vehicles-For($entry) {
    if ($entry['vehicles']) { return $entry['vehicles'] }
    if (-not $entry['vehicle']) { return $null }

    $v = [string]$entry['vehicle']
    if ($onlyVehicle -contains $v) { return $null }
    if ($bikes -contains $v) { return ($bikes -join ',') }
    if ($vans -contains $v) { return ($vans -join ',') }

    return ($cars -join ',')
}

# The pool a callout draws on, from what the callout is about. A recipe can name its own with
# models='...' and that wins - the ones that matter are the ones where the look of the person is part of
# the scene rather than scenery.
#
# sex='male' or sex='female' locks a callout to one, and is only for a callout whose own name or text
# says so: "Man with a Gun" is what the other packs call that call, so its person stays a man rather
# than the name being bent to fit the pools. Everything else is offered both.
function People-For($entry) {
    if ($entry['models']) { return $entry['models'] }

    $about = ("" + $entry['id'] + ' ' + $entry['name'] + ' ' + $entry['msg'] + ' ' + $entry['adv']).ToLowerInvariant()

    $men = $peopleCity
    $women = $womenCity

    if ($about -match 'beach|sunbathe|jetski|marina|boat|pier|swim|drown') { $men = $peopleBeach; $women = $womenBeach }
    elseif ($about -match 'ranch|farm|cattle|livestock|horse|barn|rustl|tractor|county|sandy|desert|hillbilly|moonshine|distill|poach|hunt|quarry|dirt bike|sheep|wild dog|fence|trail|hiker') { $men = $peopleCounty; $women = $womenCounty }
    elseif ($about -match 'park|ranger|camp|wildlife|logging|forest') { $men = $peopleOutdoors; $women = $womenOutdoors }
    elseif ($about -match 'construct|forklift|industrial|machinery|depot|factory|warehouse|scaffold|electric|trench|loading|site') { $men = $peopleWork; $women = $womenWork }
    elseif ($about -match 'drug|meth|deal|gang|shoot|turf|robber|burgl|mug|carjack|snatch|pickpocket|steal|stolen|graffiti|vandal|dumping|copper|theft|clown|knife|weapon|gun') { $men = $peopleRough; $women = $womenRough }

    if ($entry['sex'] -eq 'male') { return ($men -join ',') }
    if ($entry['sex'] -eq 'female') { return ($women -join ',') }

    return (($men + $women) -join ',')
}

# ============================================================ Los Santos Police
Add @{ id='lspd-armed-robbery-off-licence'; for='lspd'; name='Armed Robbery - Off Licence'; msg='Armed robbery in progress at an off licence.'; adv='One suspect with a handgun, staff inside, no shots fired yet.'; line='One suspect, handgun, in the off licence now. Get there before they leave.'; brief='Armed robbery in progress. They are still inside and the staff are still in there with them.'; app='Hostile'; armed=$true }
Add @{ id='lspd-armed-robbery-pharmacy'; for='lspd'; name='Armed Robbery - Pharmacy'; msg='Armed robbery reported at a pharmacy.'; adv='Suspect left on foot towards the estate, dark hooded top.'; line='Suspect, dark hooded top, gone on foot towards the estate.'; app='Flee'; armed=$true; res='AnyArrest' }
Add @{ id='lspd-bank-robbery'; for='lspd'; name='Bank Robbery in Progress'; msg='Silent alarm at a bank, staff not answering the phone.'; adv='Two suspects inside, cash seen being bagged.'; line='Silent alarm, no answer from the branch. Two suspects inside. Do not go in alone.'; brief='A bank, a silent alarm, and nobody answering the phone. Assume two armed and treat it that way.'; app='Hostile'; armed=$true; count=2; backup=$true }
Add @{ id='lspd-cash-in-transit'; for='lspd'; name='Cash in Transit Robbery'; msg='Armoured van crew robbed during a delivery.'; adv='Two suspects on a black motorbike, cash box taken.'; line='Cash box taken, two on a black bike. Follow it, do not force it.'; app='FleeInVehicle'; armed=$true; vehicle='akuma'; count=2 }
Add @{ id='lspd-carjacking'; for='lspd'; name='Carjacking in Progress'; msg='Driver pulled from their car at a junction.'; adv='Suspect drove off in the victim vehicle, northbound.'; line='They took the car and went north. Victim is shaken but on their feet.'; app='FleeInVehicle'; vehicle='sultan'; armed=$true }
Add @{ id='lspd-shots-fired-residential'; for='lspd'; name='Shots Fired - Residential'; msg='Several shots heard on a residential street.'; adv='No victim found by the caller, nobody answering doors.'; line='Four or five shots, no victim yet, and nobody is answering their door.'; app='HandsUp'; armed=$true; count=2 }
Add @{ sex='male'; id='lspd-man-with-a-gun'; for='lspd'; name='Man with a Gun'; msg='Man seen with a firearm at a bus stop.'; adv='No shots fired, handgun in his waistband.'; line='Caller says it is in his waistband. No shots yet.'; app='Hostile'; armed=$true }
Add @{ id='lspd-drive-by'; for='lspd'; name='Drive-By Shooting'; msg='Drive-by shooting reported from a moving vehicle.'; adv='Vehicle left, one person injured on the pavement.'; line='Car has gone, one injured on the pavement. Ambulance is rolling with you.'; amb=$true }
Add @{ id='lspd-armed-standoff'; for='lspd'; name='Armed Suspect Refusing to Comply'; msg='Armed suspect refusing to comply, officers holding back.'; adv='Suspect in a yard, weapon visible, no shots exchanged.'; line='They will not put it down and they will not come out. Back-up is two minutes out.'; app='Hostile'; armed=$true; backup=$true }
Add @{ id='lspd-drug-deal'; for='lspd'; name='Drug Deal in Progress'; msg='Drug deal in progress in an alley, caller watching.'; adv='Two suspects, exchange seen twice.'; line='Two suspects in the alley, second exchange in ten minutes.'; app='Flee' }
Add @{ id='lspd-meth-lab'; for='lspd'; name='Possible Methamphetamine Lab'; msg='Strong chemical smell from a rented property.'; adv='No persons seen, landlord reporting through the letterbox.'; line='Landlord says the smell has been there a week and nobody has been seen going in.'; backup=$true }
Add @{ id='lspd-dealing-crew'; for='lspd'; name='Street Dealing - Group'; msg='Group dealing in the open on a corner.'; adv='Three or four suspects, one keeping watch.'; line='Four of them and one watching the corner. They will scatter when they see you.'; app='Flee'; count=4; armed=$true; weapon='WEAPON_KNIFE' }
Add @{ id='lspd-burglary'; for='lspd'; name='Burglary in Progress'; msg='Neighbour reporting a break-in next door.'; adv='One suspect, rear window forced, still inside.'; line='Rear window is in and they are still inside. The neighbours are watching from their kitchen.'; app='Flee' }
Add @{ id='lspd-car-break-in'; for='lspd'; name='Vehicle Break-In'; msg='Person seen breaking into a parked car.'; adv='Suspect on foot, window smashed, owner not on scene.'; line='They smashed the window and they are still at it. Owner is not with us yet.'; app='Flee' }
Add @{ id='lspd-graffiti'; for='lspd'; name='Vandalism - Graffiti'; msg='Two people spraying graffiti on a wall.'; adv='Both on foot, cans in hand.'; line='Two of them, cans in hand, on the wall by the underpass.'; app='Flee'; count=2 }
Add @{ id='lspd-theft-in-progress'; for='lspd'; name='Theft in Progress'; msg='Person filling a bag with stock and walking out.'; adv='Staff following at a distance, no violence.'; line='They are following them at a distance. No violence so far - keep it that way.'; app='Flee' }
Add @{ id='lspd-assault-in-progress'; for='lspd'; name='Assault in Progress'; msg='Assault in progress in the street, caller watching from a window.'; adv='One suspect attacking another, both on foot.'; line='One suspect on another in the street, and the caller will not come out.'; app='Hostile'; amb=$true }
Add @{ id='lspd-stabbing'; for='lspd'; name='Stabbing Reported'; msg='Stabbing reported, victim conscious and bleeding.'; adv='Suspect ran towards the park, no weapon seen.'; line='Victim is conscious and losing blood. Suspect ran for the park, and an ambulance is coming.'; app='Flee'; amb=$true }
Add @{ id='lspd-bar-fight'; for='lspd'; name='Assault - Bar Fight'; msg='Fight outside a bar, one person on the ground.'; adv='Several involved, staff trying to separate them.'; line='Fight outside the bar, more than two involved, and one of them is down.'; app='Hostile'; count=3; amb=$true }
Add @{ id='lspd-drunk-disorderly'; for='lspd'; name='Drunk and Disorderly'; msg='Drunk and shouting at passers-by outside a bar.'; adv='No violence yet, staff want them moved on.'; line='No violence, they just want them moved on. They have other ideas.'; app='Hostile' }
Add @{ id='lspd-fight-in-street'; for='lspd'; name='Fight in the Street'; msg='Two groups fighting in the street.'; adv='Six or more involved, bottles seen.'; line='Six of them at least, and bottles. Wait for your back-up if you can.'; app='Hostile'; count=4; backup=$true; amb=$true }
Add @{ id='lspd-noise-complaint'; for='lspd'; name='Noise Complaint'; msg='Repeated noise complaint from a residential address.'; adv='Loud music, several neighbours have called.'; line='Fifth call this week from the same neighbours.'; res='Manual' }
Add @{ id='lspd-suspicious-package'; for='lspd'; name='Suspicious Package'; msg='Unattended bag reported outside a building.'; adv='No persons near it, staff clearing the building themselves.'; line='Nobody near it and the staff are clearing the building themselves. Do not go poking at it.'; res='Manual'; backup=$true }
Add @{ id='lspd-counterfeit-currency'; for='lspd'; name='Counterfeit Currency'; msg='Shop reporting a customer passing counterfeit notes.'; adv='Customer still in the shop, staff holding the note.'; line='Staff have the note and the customer is still in there. They want a report, not a scene.' }
Add @{ id='lspd-fraud-report'; for='lspd'; name='Fraud Report'; msg='Business reporting a fraudulent transaction.'; adv='Suspect left some time ago, details with staff.'; line='They are long gone but the staff have everything written down for you.'; res='Manual' }
Add @{ id='lspd-aggressive-dog'; for='lspd'; name='Aggressive Dog'; msg='Dog attacking people in a park.'; adv='Two people bitten, dog still loose.'; line='Two people bitten and the dog is still loose. Ambulance is coming for them.'; app='Hostile'; amb=$true }
Add @{ id='lspd-missing-person'; for='lspd'; name='Missing Person Report'; msg='Missing person report, last seen this morning.'; adv='Adult, no medical condition reported, on foot.'; line='Last seen this morning on foot. Family are out looking as well.'; res='Manual' }
Add @{ id='lspd-person-in-crisis'; for='lspd'; name='Person in Crisis'; msg='Caller worried about a person who has threatened to harm themselves.'; adv='Person on foot, no weapon, no crime reported.'; line='Nobody has committed an offence here. They are on their own and somebody has to talk to them.'; res='Manual' }
Add @{ id='all-officer-needs-assistance'; for='lspd,sheriff,sahp'; name='Officer Needs Assistance'; msg='Officer requesting immediate assistance, shots fired.'; adv='Officer on foot, suspect armed, no further detail.'; line='Shots fired and an officer is calling for help. Go, and go carefully.'; app='Hostile'; armed=$true; count=2; backup=$true; amb=$true }
Add @{ id='all-assault-on-officer'; for='lspd,sheriff,sahp'; name='Assault on an Officer'; msg='Officer assaulted during a stop, suspect restrained by a member of the public.'; adv='Suspect held on the ground, officer with a facial injury.'; line='A member of the public has them on the ground. The officer took one to the face.'; amb=$true }

# ============================================================ Sheriff, county
Add @{ id='lssd-ranch-trespass'; for='sheriff'; name='Trespasser on Ranch Land'; msg='Rancher reporting somebody on their land with a rifle.'; adv='Suspect on foot, rifle slung, no shots fired.'; line='They are on the rancher land with a rifle on their shoulder. Nobody has been threatened yet.'; app='HandsUp'; armed=$true; weapon='WEAPON_RIFLE' }
Add @{ id='lssd-livestock-theft'; for='sheriff'; name='Livestock Theft'; msg='Cattle reported taken from a field overnight.'; adv='Trailer tracks at the gate, no suspects present.'; line='The gate was cut and the tracks are fresh. Whoever did it is long gone.'; res='Manual' }
Add @{ id='lssd-poaching'; for='sheriff'; name='Poaching Reported'; msg='Shots heard on protected land after dark.'; adv='Vehicle parked at the treeline, no persons seen.'; line='Shots after dark and a truck at the treeline. Take it slowly, they will have rifles.'; app='Flee'; armed=$true; weapon='WEAPON_RIFLE' }
Add @{ id='lssd-rural-burglary'; for='sheriff'; name='Rural Burglary'; msg='Farmhouse broken into while the owners were out.'; adv='Rear door forced, suspects gone, owners returning.'; line='They came in through the back and they are gone. The owners are on their way home.'; res='Manual' }
Add @{ id='lssd-dirt-bikes'; for='sheriff'; name='Off-Road Bikes Reported'; msg='Group of dirt bikes riding through a residential area.'; adv='Four or five bikes, no plates, riders masked.'; line='Four or five of them, no plates, and they know every track around here.'; app='FleeInVehicle'; vehicle='sanchez'; count=3 }
Add @{ id='lssd-illegal-dumping'; for='sheriff'; name='Illegal Dumping'; msg='Van seen tipping waste on a back road.'; adv='Van still on scene, one suspect, no weapons seen.'; line='The van is still there and they are still unloading it. They have not seen anybody.'; app='FleeInVehicle'; vehicle='burrito' }
Add @{ id='lssd-desert-lab'; for='sheriff'; name='Possible Lab in the Desert'; msg='Chemical smell and generators reported at a remote property.'; adv='No persons seen, lights on at night.'; line='Generators running all night and a smell you would not forget. Nobody has seen a soul.'; backup=$true }
Add @{ id='lssd-sandy-shores-fight'; for='sheriff'; name='Fight - Sandy Shores'; msg='Fight outside a bar in Sandy Shores.'; adv='Four involved, two on the ground.'; line='Four of them and two already on the floor. It is a small town and they all know each other.'; app='Hostile'; count=4; amb=$true }
Add @{ id='lssd-domestic-county'; for='sheriff'; name='Domestic Disturbance - County'; msg='Domestic disturbance reported at a rural address.'; adv='Caller is a neighbour, shouting heard, no weapons reported.'; line='The neighbour called it in. Shouting, no weapons mentioned, and no answer from the house.'; app='Hostile'; amb=$true }
Add @{ id='lssd-moonshine-still'; for='sheriff'; name='Suspected Still'; msg='Suspected illegal distillery in the hills.'; adv='Copper tubing and drums seen from the road.'; line='Tubing, drums and a fire going. Somebody has been at this a while.'; backup=$true }
Add @{ id='lssd-wanted-person'; for='sheriff'; name='Wanted Person Seen'; msg='Wanted person seen on a trail by a hiker.'; adv='Suspect on foot, description matches a recent warrant.'; line='The hiker is sure it was them. They are on foot and they are in no hurry.'; app='Flee'; backup=$true }
Add @{ id='lssd-stolen-tractor'; for='sheriff'; name='Stolen Agricultural Vehicle'; msg='Tractor reported taken from a farm overnight.'; adv='Tracks lead towards the highway, no suspect seen.'; line='It cannot have gone far at that speed. Tracks head for the highway.' }
Add @{ id='lssd-missing-hiker'; for='sheriff,ranger'; name='Missing Hiker'; msg='Hiker overdue by several hours.'; adv='Car still at the trailhead, weather turning.'; line='Her car is still at the trailhead and the weather is coming in. That is all we have.'; res='Manual'; amb=$true }
Add @{ id='lssd-prisoner-escape'; for='sheriff,prison'; name='Prisoner Escape'; msg='Prisoner has escaped from custody during transport.'; adv='Suspect on foot in cuffs, last seen crossing the road.'; line='They are still in cuffs and they are on foot. They cannot have gone far.'; app='Flee'; backup=$true }

# ============================================================ Highway Patrol
Add @{ id='sahp-street-racing'; for='sahp'; name='Street Racing'; msg='Two vehicles racing, callers reporting high speed.'; adv='Both vehicles northbound, side by side.'; line='Two cars side by side at speed heading north. Do not race them - get in front of them.'; app='FleeInVehicle'; vehicle='sultan'; count=2 }
Add @{ id='sahp-wrong-way'; for='sahp'; name='Wrong-Way Driver'; msg='Vehicle driving the wrong way on a dual carriageway.'; adv='Multiple callers, near misses reported.'; line='Several callers, all saying the same: wrong side of the barrier, headlights on.'; app='FleeInVehicle'; vehicle='asea' }
Add @{ id='sahp-hit-and-run'; for='sahp'; name='Hit and Run'; msg='Pedestrian struck, vehicle left the scene.'; adv='Victim conscious in the road, vehicle described as a white van.'; line='White van gone, victim is in the road and conscious. Ambulance is on its way.'; amb=$true }
Add @{ id='sahp-drunk-driver'; for='sahp'; name='Drunk Driver Reported'; msg='Caller following a vehicle being driven erratically.'; adv='Vehicle swerving, speed varying between 20 and 60.'; line='The caller is behind it and says it cannot hold a lane. They will stay on the line.' }
Add @{ id='sahp-failing-to-stop'; for='sahp'; name='Vehicle Failing to Stop'; msg='Vehicle failed to stop for a patrol, last seen heading east.'; adv='Vehicle known to the caller, driver not identified.'; line='They failed to stop and went east. Call it in before you do anything.'; app='FleeInVehicle'; vehicle='premier' }
Add @{ id='sahp-insecure-load'; for='sahp'; name='Insecure Load'; msg='Lorry shedding its load on the highway.'; adv='Debris across two lanes, driver stopped ahead.'; line='Two lanes full of it and the driver has pulled up ahead. Somebody is going to hit that.'; amb=$true }
Add @{ id='sahp-road-rage'; for='sahp'; name='Road Rage Incident'; msg='Two drivers out of their cars on the hard shoulder.'; adv='Both on foot, shouting, no weapons seen.'; line='Both out of their cars on the hard shoulder. It is loud, it is not violent yet.'; app='Hostile' }
Add @{ id='sahp-breakdown'; for='sahp'; name='Broken Down Vehicle'; msg='Vehicle broken down in a live lane.'; adv='Hazards on, driver standing behind the barrier.'; line='It is in a live lane with the hazards on. Get it moved before somebody else arrives.'; res='Manual' }
Add @{ id='sahp-abandoned-vehicle'; for='sahp'; name='Abandoned Vehicle'; msg='Vehicle abandoned in the road for several days.'; adv='No occupants, no plates visible.'; line='Been there three days with no plates on it. Run it when you get there.'; res='Manual' }
Add @{ id='sahp-speeding-convoy'; for='sahp'; name='Convoy of Speeding Vehicles'; msg='Several vehicles travelling together at speed.'; adv='Three or four vehicles, no plates readable.'; line='Three or four of them running together and none of the plates readable.'; app='FleeInVehicle'; vehicle='buffalo'; count=2 }

# ============================================================ Park Rangers
Add @{ id='sapr-illegal-campfire'; for='ranger'; name='Illegal Campfire'; msg='Campfire reported in a fire-restricted area.'; adv='Group of three around it, no weapons seen.'; line='Three of them sat around a fire in a restricted area. They will not be pleased to see you.' }
Add @{ id='sapr-poaching-park'; for='ranger'; name='Poaching in the Park'; msg='Shots heard in the park after closing.'; adv='Vehicle at a fire trail, no persons seen.'; line='Shots after dark and a truck on the fire trail. Assume they are armed.'; app='Flee'; armed=$true; weapon='WEAPON_RIFLE' }
Add @{ id='sapr-lost-walker'; for='ranger'; name='Lost Walker'; msg='Walker on the phone and lost, battery nearly flat.'; adv='Knows the trailhead, does not know where they are.'; line='They are on the phone and the battery is going. We have a trailhead and nothing else.'; res='Manual' }
Add @{ id='sapr-injured-wildlife'; for='ranger'; name='Injured Wildlife'; msg='Deer struck by a vehicle, still alive at the roadside.'; adv='Animal in the road, traffic slowing.'; line='It is still on its feet and it is in the road. Drivers are slowing down to look.'; res='Manual' }
Add @{ id='sapr-off-roading'; for='ranger'; name='Illegal Off-Roading'; msg='Vehicles driving off the marked trails.'; adv='Two vehicles, tracks through protected ground.'; line='Two of them cutting through protected ground. The tracks are fresh.'; app='FleeInVehicle'; vehicle='rebel' }
Add @{ id='sapr-dumping-park'; for='ranger'; name='Dumping in the Park'; msg='Waste dumped at a trailhead overnight.'; adv='No vehicle present, no witnesses.'; line='Somebody tipped a trailer-load at the trailhead and left. No witnesses, no vehicle.'; res='Manual' }

# ============================================================ Prison
Add @{ id='saspa-escape-grounds'; for='prison'; name='Escape - Prison Grounds'; msg='Inmate missing at roll call, believed still on the grounds.'; adv='Description circulated, no vehicle reported.'; line='One missing at roll call and no vehicle reported, so they are on foot and they are close.'; app='Flee'; backup=$true }
Add @{ id='saspa-contraband-drop'; for='prison'; name='Contraband Drop'; msg='Suspected contraband drop at the perimeter.'; adv='Vehicle seen slowing at the fence, package recovered.'; line='Something came over the fence and we have it. The vehicle that threw it did not stop.'; app='FleeInVehicle'; vehicle='premier' }
Add @{ id='saspa-transport-incident'; for='prison'; name='Transport Incident'; msg='Prisoner transport stopped, inmate refusing to comply.'; adv='Inmate secured in the van, two officers on scene.'; line='The van is stopped and they will not walk. Two officers are with them and they want a hand.'; backup=$true }
Add @{ id='saspa-assault-on-staff'; for='prison'; name='Assault on Staff'; msg='Officer assaulted during a cell move.'; adv='Inmate restrained, officer with a head injury.'; line='They are restrained now but the officer took one to the head. Ambulance is coming.'; amb=$true }

# ============================================================ Medical - EMS duty
Add @{ id='ems-fall-elderly'; for='ems'; name='Fall - Elderly Resident'; msg='Elderly person fallen at home, cannot get up.'; adv='Conscious, hip pain, neighbour has the key.'; line='They have been on the floor since this morning and the neighbour has the key.'; patient='a_m_m_tramp_01'; seconds=12; pline='I cannot put any weight on it. Where is the ambulance?' }
Add @{ id='ems-diabetic'; for='ems'; name='Diabetic Emergency'; msg='Diabetic person unwell, conscious but confused.'; adv='Family present, no insulin taken today.'; line='The family are with her. She is confused and has not eaten today.'; patient='a_f_y_business_01'; seconds=14; pline='I just need something sweet. I know what it is.' }
Add @{ id='ems-seizure'; for='ems'; name='Seizure'; msg='Person having a seizure in the street.'; adv='Caller keeping people back, seizure ongoing.'; line='It is still going on. The caller is keeping everybody back like they were told to.'; patient='a_m_y_vinewood_01'; seconds=16; pline='What happened? Why is everybody looking at me?' }
Add @{ id='ems-respiratory'; for='ems'; name='Respiratory Distress'; msg='Person struggling to breathe, inhaler empty.'; adv='Conscious, sitting up, speaking in short sentences.'; line='She is sitting up and cannot finish a sentence. Inhaler is empty.'; patient='a_f_y_hipster_01'; seconds=12; pline='I can breathe again. I really could not get it in.' }
Add @{ id='ems-chest-pain'; for='ems'; name='Chest Pain'; msg='Person with chest pain, history of heart problems.'; adv='Conscious, pale, sitting on a bench.'; line='Pale, sweating, and they have a history. Do not let them walk anywhere.'; patient='a_m_m_business_01'; seconds=18; pline='It is easing. It does not feel like the last one, but it does not feel right.' }
Add @{ id='ems-assault-victim'; for='ems'; name='Assault Victim'; msg='Person assaulted, bleeding from a head wound.'; adv='Suspect gone, victim on the pavement, conscious.'; line='Suspect has gone. The victim is on the pavement and the head wound is a bad one.'; patient='a_m_y_business_01'; seconds=15; pline='I did not even see them. Is it bad?' }
Add @{ id='ems-pedestrian-struck'; for='ems'; name='Pedestrian Struck'; msg='Pedestrian struck by a vehicle, driver still on scene.'; adv='Casualty in the road, road blocked.'; line='Casualty in the road and the driver is standing there in shock. Two patients for you.'; patient='a_m_m_hasjew_01'; seconds=20; pline='My leg. I cannot feel my leg properly.' }
Add @{ id='ems-cyclist-down'; for='ems'; name='Cyclist Down'; msg='Cyclist off a bicycle, suspected fracture.'; adv='Conscious, helmet on, bicycle in the road.'; line='They came off at the junction. Helmet is still on them and they are talking.'; patient='a_m_y_skater_01'; seconds=12; pline='The bike is fine. I am not sure the arm is.' }
Add @{ id='ems-heat-exhaustion'; for='ems'; name='Heat Exhaustion'; msg='Person collapsed in the heat, no shade nearby.'; adv='Conscious but confused, no water.'; line='They have been out in it all day and there is no shade where they are.'; patient='a_m_y_beach_01'; seconds=12; pline='I just need to sit down for a minute. I am fine.' }
Add @{ id='ems-exposure'; for='ems,ranger'; name='Exposure - Cold'; msg='Walker found cold and disoriented on the ridge.'; adv='Shivering stopped, caller has them out of the wind.'; line='They are out of the wind with the caller. The shivering has stopped, which is not good news.'; patient='a_m_m_farmer_01'; seconds=14; pline='I lost the path in the fog. I thought I was going the right way.' }
Add @{ id='ems-overdose-park'; for='ems'; name='Overdose in a Park'; msg='Person unresponsive on a bench, paraphernalia nearby.'; adv='Breathing but not waking, no witnesses.'; line='Breathing but not waking, and nobody with them. You are the first one there.'; patient='a_m_m_trampbeac_01'; seconds=12; pline='Where - what day is it? Did somebody call you?' }
Add @{ id='ems-fall-from-height'; for='ems'; name='Fall from Height'; msg='Tradesman fallen from a ladder, conscious.'; adv='Casualty on the ground, colleague applying pressure.'; line='They went off the third rung carrying something. Their mate is holding pressure.'; patient='a_m_y_construct_01'; seconds=16; pline='I landed on my side. I can move my fingers. That is something.' }
Add @{ id='ems-domestic-injury'; for='ems'; name='Injury - Domestic'; msg='Person with an injury following a domestic incident.'; adv='Patient safe with a neighbour, injury to the arm.'; line='She is next door with the neighbour. The injury needs looking at and so does she.'; patient='a_f_y_vinewood_01'; seconds=14; pline='It is not as bad as it looks. They have gone.' }
Add @{ id='ems-broken-ankle'; for='ems,ranger'; name='Walker with a Broken Ankle'; msg='Walker unable to continue, suspected broken ankle.'; adv='Ridge path, no road access, weather closing in.'; line='She is on the ridge path and there is no road up there. Take the kit you can carry.'; patient='a_f_y_hipster_01'; seconds=18; pline='I heard it go. I am not walking down from here.' }

# ============================================================ Fire duty
Add @{ id='fire-refuse-fire'; for='fire'; name='Refuse Fire'; msg='Bins on fire behind a row of shops.'; adv='Two bins alight, no structures involved.'; line='Two bins behind the shops. Get there before it reaches the fence.'; app='Fire' }
Add @{ id='fire-kitchen-fire'; for='fire'; name='Kitchen Fire'; msg='Kitchen fire in a flat, occupants out.'; adv='Occupants evacuated by a neighbour, smoke from the window.'; line='Everybody is out and the neighbour did the smoke alarm part. Smoke from the back window.'; app='Fire' }
Add @{ id='fire-grass-fire'; for='fire'; name='Grass Fire'; msg='Grass fire on the hillside, wind picking up.'; adv='No property at risk yet, walking trail nearby.'; line='Hillside, wind getting up, and there is a walking trail on the other side of it.'; app='Fire' }
Add @{ id='fire-vehicle-car-park'; for='fire'; name='Vehicle Fire'; msg='Vehicle alight in a car park, one person overcome by smoke.'; adv='Nobody in the vehicle, one casualty being kept back.'; line='Nobody is in the car. One person took the smoke and is being kept clear.'; app='Fire'; patient='a_m_y_business_01'; seconds=10; pline='I was right next to it. My chest feels tight.' }
Add @{ id='fire-commercial-bins'; for='fire'; name='Commercial Rubbish Fire'; msg='Fire in the bins behind a business, spreading to a fence.'; adv='Staff using extinguishers, fence beginning to catch.'; line='Staff have extinguishers out but the fence is starting to go. Get there.'; app='Fire' }
Add @{ id='fire-vehicle-roadside'; for='fire'; name='Vehicle Fire - Roadside'; msg='Car alight on the hard shoulder, occupants out.'; adv='Occupants well clear, traffic passing slowly.'; line='Everybody is out of it and standing well back. Traffic is slowing to look, which is its own problem.'; app='Fire' }

# ============================================================ The other packs' callouts, so they can come out
#
# Every callout 686 Callouts 2.1.2 offers (its thirty English translation files) and every one United
# Callouts enables, as close as a recipe can get to each. Where the original is a scripted set-piece -
# a car bomb on a timer, a protest with a crowd - this is the same call with a simpler scene: the point
# is that nothing is lost from the callout list when the other packs are uninstalled, not that the
# staging is identical.

# --- 686 Callouts, its thirty
Add @{ id='p686-alleyway-robbery'; for='lspd'; name='Alleyway Robbery'; msg='Robbery in progress in an alley, caller watching from a window.'; adv='One suspect, knife seen, victim still there.'; line='Knife, one suspect, and the victim is still in the alley with them.'; app='Flee'; armed=$true; weapon='WEAPON_KNIFE'; amb=$true }
Add @{ id='p686-area-surveillance'; for='lspd'; name='Area Surveillance'; msg='Request for surveillance of an area after repeated reports.'; adv='No crime in progress, several complaints logged.'; line='Nothing to attend as such - show yourself in the area and see what is going on.'; res='Manual' }
Add @{ id='p686-attempted-break-in'; for='lspd'; name='Attempted Break-In'; msg='Attempted break-in reported, offender disturbed.'; adv='Rear window damaged, offender gone on foot.'; line='They were disturbed and they ran. The window is damaged but nobody is inside.'; app='Flee' }
Add @{ id='p686-car-bomb'; for='lspd,fire'; name='Suspected Vehicle Bomb'; msg='Vehicle reported with wiring visible under the dashboard.'; adv='Nobody in or near the vehicle, caller keeping people back.'; line='Wiring under the dash and nobody near it. Do not touch the car - keep the street clear.'; res='Manual'; backup=$true }
Add @{ id='p686-custom-pursuit'; for='lspd'; name='Pursuit in Progress'; msg='Vehicle failing to stop, pursuit not yet authorised.'; adv='Vehicle heading west at speed, driver not identified.'; line='They are not stopping and they are going west. Call it in before you commit.'; app='FleeInVehicle'; vehicle='buffalo' }
Add @{ id='p686-disorientated-individual'; for='lspd,ems'; name='Disorientated Individual'; msg='Person wandering in the road, disorientated.'; adv='No weapon seen, person cannot say where they live.'; line='They are in the road and they do not know where they are. Could be medical, could be drink.'; res='Manual'; amb=$true }
Add @{ id='p686-fare-dodger'; for='lspd'; name='Fare Evader'; msg='Passenger refusing to pay and refusing to leave a bus.'; adv='Driver holding the bus at a stop, passenger on board.'; line='The bus is stopped and they will not get off it. Driver wants them dealt with, not arrested.'; app='Flee' }
Add @{ id='p686-high-risk-escort'; for='lspd,sheriff'; name='High Risk Escort'; msg='Escort required for a high-risk transfer.'; adv='Transfer waiting on you, route already cleared.'; line='They will not move until you are with them. It is a straight run and they want it done quietly.'; res='Manual'; backup=$true }
Add @{ id='p686-kidnapping'; for='lspd'; name='Kidnapping in Progress'; msg='Person forced into a vehicle, caller following at a distance.'; adv='Two suspects, victim in the back seat, vehicle still moving.'; line='They put somebody in the back of it and drove. The caller is behind them and staying on the line.'; app='FleeInVehicle'; armed=$true; count=2; backup=$true }
Add @{ id='p686-lane-closure'; for='sahp'; name='Lane Closure'; msg='Request to close a lane for a recovery.'; adv='Recovery truck on scene, traffic down to one lane.'; line='The truck is there and the traffic is backing up. Somebody needs to stand in the road.'; res='Manual' }
Add @{ id='p686-large-vehicle-pursuit'; for='lspd,sahp'; name='Large Vehicle Failing to Stop'; msg='HGV failing to stop, driver ignoring lights.'; adv='Vehicle continuing at speed, load insecure.'; line='They are not stopping and there is a load on the back of it. Keep your distance.'; app='FleeInVehicle'; vehicle='phantom' }
Add @{ id='p686-offensive-weapon'; for='lspd'; name='Offensive Weapon Reported'; msg='Person reported carrying a weapon in public.'; adv='Suspect on foot, bat or club seen, no victims.'; line='Bat in their hand and no victims yet. They are walking, not running.'; app='HandsUp'; armed=$true; weapon='WEAPON_BAT' }
Add @{ id='p686-photography'; for='lspd'; name='Photographing a Police Station'; msg='Person photographing a police station, reported by staff.'; adv='Suspect on foot with a camera, no offence reported.'; line='Nothing has happened, they are just taking pictures of the building. Staff want them spoken to.'; res='Manual' }
Add @{ id='p686-property-checkup'; for='lspd,sheriff'; name='Property Check'; msg='Alarm activated at a property, no answer from the keyholder.'; adv='No persons seen, no forced entry visible.'; line='Alarm has been going ten minutes and the keyholder is not answering. Could be nothing.'; res='Manual' }
Add @{ id='p686-protest'; for='lspd'; name='Protest Reported'; msg='Protest gathering outside a building.'; adv='Around twenty people, no violence reported.'; line='Loud but peaceful so far. Show a presence and keep the road open.'; res='Manual'; backup=$true }
Add @{ id='p686-solicitation'; for='lspd'; name='Solicitation Reported'; msg='Solicitation reported on a street corner.'; adv='Fesuspect on foot, no violence, businesses complaining.'; line='Businesses have had enough. Nobody is in danger, they just want it dealt with.'; res='Manual' }
Add @{ id='p686-sting-operation'; for='lspd'; name='Sting Operation'; msg='Sting in progress, suspects about to arrive.'; adv='Two vehicles expected, plain-clothes officers already in place.'; line='Everybody is in position and they are expecting two vehicles. Do not roll up in front of them.'; app='Hostile'; armed=$true; count=2; backup=$true }
Add @{ id='p686-stolen-item'; for='lspd'; name='Stolen Item Reported'; msg='Stolen item seen in a pawn shop window.'; adv='Staff cooperating, item matches a recent report.'; line='It matches the description exactly and the staff are being helpful about it.'; res='Manual' }
Add @{ id='p686-stolen-pedal-bike'; for='lspd'; name='Stolen Pedal Cycle'; msg='Bicycle stolen from outside a shop minutes ago.'; adv='Suspect rode off towards the park, description given.'; line='They rode off on it towards the park five minutes ago. They will not have gone far.'; app='Flee' }
Add @{ id='p686-subway-disturbance'; for='lspd'; name='Subway Disturbance'; msg='Disturbance on a subway platform.'; adv='Two suspects arguing, staff asking for someone to attend.'; line='Two of them on the platform and the staff have had enough. It has not turned violent.'; app='Hostile' }
Add @{ id='p686-suspected-stalker'; for='lspd'; name='Suspected Stalker'; msg='Person repeatedly seen watching an address.'; adv='Person in a parked car, no offence witnessed yet.'; line='Third report this week, same car, same spot. They are there now.'; res='Manual' }
Add @{ id='p686-swat-raid'; for='lspd,swat'; name='SWAT Raid'; msg='Raid authorised on an address, occupants refusing to come out.'; adv='Two inside, weapons believed present, entry team waiting on your call.'; line='They will not come out and the entry team is waiting on you. Two inside, and they are armed.'; app='Hostile'; armed=$true; count=2; backup=$true }
Add @{ id='p686-terrorist-attack'; for='any'; name='Terrorist Attack'; msg='Attack in progress, multiple callers, shots reported.'; adv='Several armed, casualties reported, area not secured.'; line='Multiple callers, shots, and casualties. Nobody has secured anything yet - go in carefully.'; app='Hostile'; armed=$true; count=3; backup=$true; amb=$true }
Add @{ id='p686-traffic-stop-backup'; for='lspd,sahp,sheriff'; name='Traffic Stop Backup'; msg='Officer requesting backup at a traffic stop.'; adv='Single officer on scene, driver not complying.'; line='One officer on scene and the driver is not doing what they are told. Get there.'; res='Manual'; backup=$true }
Add @{ id='p686-vehicle-on-sidewalk'; for='lspd'; name='Vehicle on the Pavement'; msg='Vehicle driven onto the pavement, pedestrians having to step into the road.'; adv='Driver still with the vehicle, no contact made.'; line='They have parked it across the pavement and they are still sat in it.'; res='Manual' }
Add @{ id='p686-vip-evacuation'; for='lspd'; name='VIP Evacuation'; msg='Protective detail requesting an evacuation route.'; adv='Principal in a vehicle, route not yet clear.'; line='They need the route clear before they move. It is a two-minute job if the road is empty.'; res='Manual'; backup=$true }
Add @{ id='p686-aircraft-in-distress'; for='any'; name='Aircraft in Distress'; msg='Light aircraft reported in difficulty, may be attempting to land.'; adv='Smoke reported from the engine, pilot still in contact.'; line='Smoke from the engine and they are trying to put it down. Wherever they land, we are going.'; res='Manual'; amb=$true }

# --- United Callouts, its enabled nineteen
Add @{ id='uc-stolen-emergency-vehicle'; for='lspd,sheriff'; name='Stolen Emergency Vehicle'; msg='Ambulance stolen from outside a hospital.'; adv='Vehicle heading north, driver in a uniform.'; line='Somebody has taken an ambulance from the hospital bay. Blue lights, no crew.'; app='FleeInVehicle'; vehicle='ambulance' }
Add @{ id='uc-money-truck-theft'; for='lspd'; name='Money Truck Theft'; msg='Armoured truck being broken into, guards held.'; adv='Two suspects, both armed, truck stopped.'; line='They have stopped the truck and they are working on the back of it. Two, both armed.'; app='Hostile'; armed=$true; count=2 }
Add @{ id='uc-stolen-bus'; for='lspd'; name='Stolen Bus'; msg='Bus taken from a depot, driver refusing to stop.'; adv='Bus heading through the city, no passengers on board.'; line='They took it empty, so there is nobody on board to worry about. They are still going.'; app='FleeInVehicle'; vehicle='bus' }
Add @{ id='uc-stolen-commercial-vehicle'; for='lspd,sheriff'; name='Stolen Commercial Vehicle'; msg='Flatbed truck taken from a yard.'; adv='Vehicle loaded with plant equipment, heading out of the city.'; line='They took the truck with the equipment still on the back. They will be heading out of town.'; app='FleeInVehicle'; vehicle='flatbed' }
Add @{ id='uc-gang-shootout'; for='lspd'; name='Gang Shootout'; msg='Shots exchanged between two groups.'; adv='Four or more involved, at least one injured.'; line='Two groups and they are shooting at each other. Wait for back-up if you can.'; app='Hostile'; armed=$true; count=4; backup=$true; amb=$true }
Add @{ id='uc-armed-clown'; for='lspd'; name='Person in Clown Mask with a Weapon'; msg='Person in a mask carrying a weapon, frightening the public.'; adv='Person in a clown mask, weapon visible, no victims.'; line='They are in a mask and they have something in their hand, and they are enjoying the reaction.'; app='Hostile'; armed=$true; weapon='WEAPON_BAT' }
Add @{ id='uc-person-with-knife'; for='lspd'; name='Person with a Knife'; msg='Person in the street with a knife, public backing away.'; adv='Suspect on foot, knife in hand, no victims yet.'; line='Knife in their hand and people are backing away from them. Nobody has been hurt yet.'; app='Hostile'; armed=$true; weapon='WEAPON_KNIFE' }
Add @{ id='uc-warrant-for-arrest'; for='lspd,sheriff'; name='Warrant for Arrest'; msg='Wanted person believed at an address.'; adv='Name and description confirmed, occupant uncooperative.'; line='Address confirmed and the warrant is live. Nobody has answered the door but somebody is in.'; res='Manual'; backup=$true }
Add @{ id='uc-metro-disturbance'; for='lspd'; name='Disturbance at a Metro Station'; msg='Disturbance at a metro station, staff asking for attendance.'; adv='Three involved, no weapons seen.'; line='Three of them and the staff want them out. No weapons mentioned.'; app='Hostile'; count=3 }
Add @{ id='uc-cyclist-motorway'; for='sahp'; name='Cyclist on the Highway'; msg='Cyclist riding on the highway, drivers reporting near misses.'; adv='Single cyclist in a live lane, no lights.'; line='They are in a live lane with no lights on them. Somebody is going to hit them.'; res='Manual' }
Add @{ id='uc-atm-activity'; for='lspd'; name='Suspicious Activity at an ATM'; msg='People loitering around an ATM with tools.'; adv='Two suspects, tools visible, no offence seen yet.'; line='Two of them and something long in a bag. They have not touched the machine yet.'; app='Flee'; count=2 }
Add @{ id='uc-k9-backup'; for='lspd,sheriff'; name='K9 Backup Required'; msg='Handler requesting a K9 unit, suspect fled on foot.'; adv='Suspect in a yard, no weapon seen, handler holding the perimeter.'; line='They are in the yard and the handler wants a dog rather than a foot chase. Hold what you have.'; res='Manual'; backup=$true }

# --- the story-mission remakes, as police work
Add @{ id='story-jewel-store-job'; for='lspd'; name='Jewellery Store Robbery'; msg='Smash and grab at a jewellery store, alarm activated.'; adv='Three suspects, mopeds waiting outside.'; line='They are in the shop and there are bikes waiting outside. Nobody has stopped them yet.'; app='Flee'; armed=$true; count=3 }
Add @{ id='story-armoured-truck'; for='lspd'; name='Armoured Truck Intercepted'; msg='Armoured truck stopped in the road, crew not responding.'; adv='Truck abandoned, crew unaccounted for.'; line='The truck is stopped and nobody is answering from it. Approach like they are still inside.'; app='Hostile'; armed=$true; backup=$true }
Add @{ id='story-hood-safari'; for='sheriff'; name='Hunters Reported'; msg='Armed suspects in the county, residents reporting shots.'; adv='Two vehicles, rifles seen, one resident confronted.'; line='They have rifles and one of them has already had a go at a resident. Both vehicles are still close.'; app='Hostile'; armed=$true; weapon='WEAPON_RIFLE'; count=2; backup=$true }
Add @{ id='story-bank-heist-aftermath'; for='lspd'; name='Heist Aftermath'; msg='Bank robbery abandoned, suspects fleeing on foot.'; adv='Cash on the pavement, three suspects running.'; line='They have dropped half of it and they are running. The money is not the priority.'; app='Flee'; armed=$true; count=3; backup=$true }

# ============================================================ More of everything
#
# The second half of the library: more situations for every duty, so that a patrol of any kind has
# enough variety that the same call does not come round twice in a shift.

# --- LSPD, city work
Add @{ id='lspd-mugging'; for='lspd'; name='Street Robbery'; msg='Street robbery just happened, victim still on scene.'; adv='Two suspects on foot, phone and wallet taken.'; line='Two of them, took the phone and ran. The victim is still there and shaken.'; app='Flee'; count=2; amb=$true }
Add @{ id='lspd-pickpocket'; for='lspd'; name='Pickpocket Reported'; msg='Pickpocket seen working a crowd.'; adv='One suspect, hands in and out of pockets, no violence.'; line='They are working the queue outside the station. Nobody has noticed except the caller.'; app='Flee' }
Add @{ id='lspd-copper-theft'; for='lspd'; name='Cable Theft in Progress'; msg='Person cutting cable from a substation.'; adv='One suspect with tools, no power lines down.'; line='They are cutting the cable off the wall. Do not touch the metalwork.' }
Add @{ id='lspd-shoplifting-crew'; for='lspd'; name='Organised Shoplifting'; msg='Group filling bags and walking out of a store.'; adv='Three of them, staff told not to intervene.'; line='Three of them, bags full, and the staff have been told not to get involved. Which leaves it to you.'; app='Flee'; count=3 }
Add @{ id='lspd-stolen-motorbike'; for='lspd'; name='Stolen Motorcycle'; msg='Motorcycle taken from outside a cafe minutes ago.'; adv='Two riders, both in dark clothing, no plates.'; line='Two on it, no plates, gone towards the freeway. They will not stop for you.'; app='FleeInVehicle'; vehicle='bati' }
Add @{ id='lspd-catalytic-theft'; for='lspd'; name='Catalytic Converter Theft'; msg='Person under a parked car with a saw.'; adv='One suspect, car jacked up, no owner present.'; line='They have it up on a jack and they are under it with a saw. Owner is not coming.'; app='Flee' }
Add @{ id='lspd-car-meet'; for='lspd,sahp'; name='Car Meet Nuisance'; msg='Large car meet blocking a retail park.'; adv='Around fifteen vehicles, engines revving, no racing yet.'; line='Fifteen cars and nobody is moving. They will scatter the moment you arrive - which is half the problem.'; res='Manual'; backup=$true }
Add @{ id='lspd-illegal-vendor'; for='lspd'; name='Illegal Street Vendor'; msg='Unlicensed vendor blocking the pavement.'; adv='One suspect, stall set up, no violence.'; line='Pavement blocked, no licence, and they have been told twice already.'; res='Manual' }
Add @{ id='lspd-public-intoxication'; for='lspd'; name='Public Intoxication'; msg='Person asleep in a doorway, very drunk.'; adv='Suspect, no injuries seen, businesses complaining.'; line='They are asleep in the doorway and they smell like a brewery. Do not let them sleep it off in this weather.'; res='Manual'; amb=$true }
Add @{ id='lspd-indecent-exposure'; for='lspd'; name='Indecent Exposure'; msg='Person reported exposing themselves in a park.'; adv='Suspect on foot, no other parties involved.'; line='In the park, on their own, and the caller is not making it up. Go and deal with them.'; app='Flee' }
Add @{ id='lspd-missing-child'; for='lspd'; name='Missing Child'; msg='Child reported missing from a shopping centre.'; adv='Seven years old, last seen ten minutes ago, parent with staff.'; line='Seven years old and missing ten minutes in a shopping centre. Staff are checking cameras now.'; res='Manual'; backup=$true; amb=$true }
Add @{ id='lspd-child-neglect'; for='lspd'; name='Child Welfare Concern'; msg='Children left alone at an address, neighbour concerned.'; adv='Two children, no adults seen for hours.'; line='Nobody has been in or out for most of the day and the children are on their own. The neighbour is sure.'; res='Manual'; backup=$true }
Add @{ id='lspd-elder-abuse'; for='lspd'; name='Concern for an Elderly Resident'; msg='Care worker reporting bruising on an elderly client.'; adv='Client on scene, carer present, no violence reported.'; line='The care worker is describing bruising nobody has explained. Take it seriously and take your time.'; res='Manual'; amb=$true }
Add @{ id='lspd-illegal-firearm-sale'; for='lspd'; name='Firearm Sale Reported'; msg='Two suspects exchanging a weapon in a car park.'; adv='One weapon seen, in a bag, no shots fired.'; line='A bag, a gun and money changing hands. Nobody has fired anything yet.'; app='Flee'; armed=$true; count=2 }
Add @{ id='lspd-gang-intimidation'; for='lspd'; name='Gang Intimidation'; msg='Group intimidating a shop owner on the doorstep.'; adv='Three suspects, no weapons seen, shop owner frightened.'; line='Three of them at their door and they are on their own inside. Nobody has been hit yet.'; app='Hostile'; count=3 }
Add @{ id='lspd-taxi-assault'; for='lspd'; name='Assault on a Driver'; msg='Taxi driver assaulted by a passenger.'; adv='Driver on scene with a facial injury, suspect gone on foot.'; line='Passenger has gone and the driver has taken one to the face over a fare.'; app='Flee'; amb=$true }
Add @{ id='lspd-bus-assault'; for='lspd'; name='Assault on a Bus'; msg='Passenger assaulted on a bus, driver holding the vehicle.'; adv='Suspect on board, victim with staff, no weapon seen.'; line='The driver has stopped the bus and the suspect is still on it. Victim is with the other passengers.'; res='Manual'; amb=$true }
Add @{ id='lspd-vandalism-school'; for='lspd'; name='Vandalism at a School'; msg='Windows broken at a school overnight, caretaker reporting.'; adv='Nobody on scene, damage to four windows.'; line='Four windows and nobody about. The caretaker will meet you there.'; res='Manual' }
Add @{ id='lspd-fireworks'; for='lspd'; name='Fireworks Complaint'; msg='Fireworks being set off in a residential street.'; adv='Group of four, fireworks in a bin, no injuries.'; line='Four of them letting them off down the middle of the road. Somebody will lose a hand at this rate.'; app='Flee'; count=4 }
Add @{ id='lspd-false-alarm'; for='lspd'; name='Alarm Activation'; msg='Commercial alarm activated, keyholder on the way.'; adv='No persons seen, building secure from the front.'; line='Probably nothing - but the last three at this address were not.'; res='Manual' }
Add @{ id='lspd-drone-over-prison'; for='lspd,prison'; name='Drone Over the Perimeter'; msg='Drone seen over a secure perimeter.'; adv='Drone still airborne, operator believed nearby.'; line='Somebody is flying a drone over the fence and they are not doing it for the view.'; app='Flee'; backup=$true }

# --- Sheriff, county
Add @{ id='lssd-barn-fire'; for='sheriff,fire'; name='Barn Fire'; msg='Barn alight on farmland, livestock inside.'; adv='Owner on scene, animals still inside, no persons hurt.'; line='The barn is well alight and there are animals in it. The owner is trying to get them out themselves.'; app='Fire'; amb=$true }
Add @{ id='lssd-horse-loose'; for='sheriff'; name='Horse Loose on the Road'; msg='Horse loose on a county road, drivers swerving.'; adv='Animal in the road, owner looking for it.'; line='It is in the road and drivers are swerving round it. The owner is out looking.'; res='Manual' }
Add @{ id='lssd-firearms-stolen'; for='sheriff'; name='Firearms Stolen from a Farm'; msg='Shotgun cabinet forced at a farmhouse.'; adv='Two weapons taken, owners were out.'; line='Cabinet forced, two gone. Whoever did it knew when they would be out.'; res='Manual'; backup=$true }
Add @{ id='lssd-trailer-meth'; for='sheriff'; name='Suspicious Trailer'; msg='Trailer on a back road with a chemical smell.'; adv='No persons seen, generator running.'; line='Generator going and a smell that is not farmyard. Nobody about.'; backup=$true }
Add @{ id='lssd-domestic-firearms'; for='sheriff'; name='Domestic with Firearms'; msg='Domestic incident, caller says there are guns in the house.'; adv='One suspect inside, family outside with a neighbour.'; line='The family are out and they say there are guns in the house. They are still in there.'; app='Hostile'; armed=$true; backup=$true; amb=$true }
Add @{ id='lssd-ranch-hand-missing'; for='sheriff'; name='Missing Ranch Hand'; msg='Ranch hand not returned from checking fences.'; adv='On foot, no phone signal in the area.'; line='They went out to check fences this morning and they have not come back. No signal out there.'; res='Manual'; amb=$true }
Add @{ id='lssd-illegal-logging'; for='sheriff,ranger'; name='Illegal Logging'; msg='Chainsaws and a flatbed working in protected woodland.'; adv='Two vehicles, equipment running, no persons confronted.'; line='Two trucks and chainsaws going. They are not supposed to be in there at all.'; app='Flee'; backup=$true }
Add @{ id='lssd-hunter-shot'; for='sheriff,ems'; name='Hunter Injured'; msg='Hunter injured in the field, another hunter on the phone.'; adv='Gunshot wound to the leg, conscious, remote location.'; line='Shot in the leg and they are a long way from a road. The ambulance is not going to reach them on its own.'; res='Manual'; amb=$true }
Add @{ id='lssd-livestock-attack'; for='sheriff,ranger'; name='Livestock Attacked'; msg='Sheep attacked overnight, predator believed still nearby.'; adv='Several dead, owner asking for someone to attend.'; line='Something has been through the flock and the owner thinks it is still about.'; res='Manual' }
Add @{ id='lssd-hunters-trespass'; for='sheriff'; name='Hunters on Private Land'; msg='Two hunters with rifles on land they have no permission for.'; adv='Both on foot, rifles slung, landowner confronting them.'; line='They are on their land with rifles and they are out there arguing with them. Get there before they lose their temper.'; app='Hostile'; armed=$true; weapon='WEAPON_RIFLE'; count=2 }
Add @{ id='lssd-still-fire'; for='sheriff,fire'; name='Fire at a Remote Property'; msg='Fire at an outbuilding on a remote property.'; adv='No persons seen, smoke visible from the road.'; line='Smoke you can see from the highway and nobody answering. It could be the still going up.'; app='Fire' }

# --- Highway Patrol
Add @{ id='sahp-motorcycle-racing'; for='sahp'; name='Motorcycle Racing'; msg='Motorcycles racing on the highway.'; adv='Three bikes, no plates, weaving between traffic.'; line='Three of them between the lanes and none of them with plates. Do not try to match them.'; app='FleeInVehicle'; vehicle='bati'; count=3 }
Add @{ id='sahp-overweight-lorry'; for='sahp'; name='Overweight Lorry'; msg='Lorry stopped for a weight check, driver abusive.'; adv='Vehicle on the weighbridge, driver out of the cab.'; line='They are out of the cab and they are not interested in the weighbridge. The paperwork is probably worse than the weight.'; app='Hostile' }
Add @{ id='sahp-debris'; for='sahp'; name='Debris in the Road'; msg='Debris across two lanes, source unknown.'; adv='No vehicles stopped nearby, several near misses.'; line='Nobody knows where it came from and three people have nearly hit it. Somebody has to stand there.'; res='Manual' }
Add @{ id='sahp-jackknife'; for='sahp'; name='Jackknifed Lorry'; msg='Articulated lorry jackknifed across the carriageway.'; adv='Driver out and unhurt, carriageway blocked both ways.'; line='It is across both lanes and the driver is out. Nobody is hurt, everything is stuck.'; res='Manual'; amb=$true }
Add @{ id='sahp-cow-motorway'; for='sahp,sheriff'; name='Animal on the Carriageway'; msg='Cow on the carriageway, drivers stopping.'; adv='Animal wandering, no owner present.'; line='There is a cow on the carriageway and drivers are stopping to film it. Clear the traffic first.'; res='Manual' }
Add @{ id='sahp-cyclist-collision'; for='sahp,ems'; name='Cyclist Collision'; msg='Cyclist struck by a car, driver on scene.'; adv='Casualty in the road, driver cooperating, lane blocked.'; line='Cyclist down and the driver is with them, and they are not moving their arm.'; patient='a_m_y_skater_01'; seconds=16; pline='My shoulder. I went over the bonnet.' }
Add @{ id='sahp-roadworks'; for='sahp'; name='Incident at Roadworks'; msg='Vehicle has gone through a roadworks closure.'; adv='No injuries, cones scattered, workers shaken.'; line='Nobody is hurt, which is remarkable. The workers want them found.'; app='FleeInVehicle'; vehicle='dilettante' }
Add @{ id='sahp-tunnel-fire'; for='fire,sahp'; name='Vehicle Fire in a Tunnel'; msg='Vehicle alight inside a tunnel, traffic still moving.'; adv='Occupants out, smoke filling the tunnel.'; line='They are out of it but the smoke has nowhere to go and people are still driving in.'; app='Fire'; amb=$true }

# --- Rangers
Add @{ id='sapr-bear-sighting'; for='ranger'; name='Bear Sighting'; msg='Bear reported near a campsite, campers concerned.'; adv='Animal not aggressive, campers still on site.'; line='It has not gone for anybody yet. Move the campers on before it changes its mind.'; res='Manual' }
Add @{ id='sapr-campers-fire'; for='ranger,fire'; name='Illegal Fire at a Campsite'; msg='Large fire at a campsite in a restricted area.'; adv='Six campers, fire well established, no injuries.'; line='Six of them around a fire that is far bigger than the rules allow. Wind is coming up.'; app='Fire' }
Add @{ id='sapr-illegal-fishing'; for='ranger'; name='Illegal Fishing'; msg='People netting a protected lake.'; adv='Two suspects with nets and a cooler, vehicle nearby.'; line='Nets in a lake they should not be anywhere near. Vehicle is parked on the trail.'; app='Flee'; count=2 }
Add @{ id='sapr-climber-stuck'; for='ranger,ems'; name='Climber Stuck on a Ledge'; msg='Climber unable to move, below the ridge.'; adv='Conscious, one arm injured, no way down alone.'; line='They are on a ledge and cannot get off it, and there is no road within a mile of them.'; res='Manual'; amb=$true }
Add @{ id='sapr-tourist-lost'; for='ranger'; name='Tourists Lost on a Trail'; msg='Family of four lost on a trail, phone at 4% battery.'; adv='They can describe a waterfall and a fence.'; line='They have a waterfall and a fence and about four per cent of battery left.'; res='Manual'; amb=$true }
Add @{ id='sapr-snake-bite'; for='ranger,ems'; name='Snake Bite'; msg='Walker bitten by a snake on a trail.'; adv='Conscious, bite to the calf, remote location.'; line='Bitten on the leg and a long way from the road. Keep them still and get to them.'; patient='a_m_y_hipster_01'; seconds=14; pline='It is going numb. Is that normal?' }

# --- Prison
Add @{ id='saspa-drone-drop'; for='prison'; name='Drone Drop'; msg='Drone seen dropping a package inside the perimeter.'; adv='Package recovered, drone gone.'; line='It came over the wall and dropped it. The drone is gone and the package is ours.'; res='Manual' }
Add @{ id='saspa-escape-vehicle'; for='prison,sheriff'; name='Escape Vehicle Reported'; msg='Vehicle seen taking on a passenger at the perimeter road.'; adv='Vehicle heading for the highway, one passenger boarded.'; line='Somebody got into it at the fence line and it went for the highway. That is an escape attempt in progress.'; app='FleeInVehicle'; vehicle='premier'; backup=$true }
Add @{ id='saspa-visitor-contraband'; for='prison'; name='Contraband with a Visitor'; msg='Visitor found with contraband at the gate.'; adv='Visitor detained, item recovered, no violence.'; line='They found it at the gate and the visitor is still in the search room and is not saying who it was for.'; res='Manual' }
Add @{ id='saspa-medical-emergency'; for='prison,ems'; name='Medical Emergency - Inmate'; msg='Inmate collapsed in a workshop.'; adv='Conscious but unwell, staff on scene, no violence.'; line='They went down in the workshop and the staff have them sitting up. They want the ambulance and so do I.'; patient='s_m_m_prisguard_01'; seconds=14; pline='I came over dizzy. I have not felt right since breakfast.' }

# --- EMS, more patients
Add @{ id='ems-stroke'; for='ems'; name='Suspected Stroke'; msg='Person with slurred speech and a weak side.'; adv='Conscious, family present, onset twenty minutes ago.'; line='Face dropped and one side weak, twenty minutes ago. That is a stroke clock and it is running.'; patient='a_m_m_business_01'; seconds=14; pline='My arm went heavy. I could not hold the phone.' }
Add @{ id='ems-allergic-reaction'; for='ems'; name='Allergic Reaction'; msg='Person swollen after eating, struggling to swallow.'; adv='Conscious, epinephrine not used, restaurant staff on scene.'; line='Swelling fast and she has not used a pen. This is the one that goes wrong if nobody gets there.'; patient='a_f_y_vinewood_01'; seconds=12; pline='It is going down. I can swallow again.' }
Add @{ id='ems-burn-injury'; for='ems,fire'; name='Burn Injury'; msg='Person with burns to the arms from a kitchen.'; adv='Conscious, burns to both forearms, kitchen staff on scene.'; line='Both forearms and they are holding them out like they cannot decide what to do. Cool it and get them moving.'; patient='a_m_y_business_02'; seconds=15; pline='I poured it over myself. It was on the stove.' }
Add @{ id='ems-industrial-accident'; for='ems'; name='Industrial Accident'; msg='Worker injured by machinery at a depot.'; adv='Conscious, hand caught, machinery stopped.'; line='Their hand was in it when it went. The machinery is stopped and so is the bleeding, for now.'; patient='a_m_y_construct_02'; seconds=18; pline='I cannot see how bad it is. Tell me how bad it is.' }
Add @{ id='ems-dog-bite'; for='ems'; name='Dog Bite'; msg='Person bitten by a dog, wound to the leg.'; adv='Conscious, dog secured by the owner, wound bleeding.'; line='Dog is secured and the owner is apologetic, which does not help the leg.'; patient='a_f_y_business_01'; seconds=12; pline='It just went for me. I did not even touch it.' }
Add @{ id='ems-drowning'; for='ems,ranger'; name='Near Drowning'; msg='Person pulled from the water, not breathing.'; adv='Bystanders started compressions, water still in the lungs.'; line='Pulled out of the water and somebody is doing compressions on them. Do not stop until you have to.'; patient='a_m_y_beach_03'; seconds=20; pline='(a cough, then breathing)' }
Add @{ id='ems-electric-shock'; for='ems'; name='Electric Shock'; msg='Person shocked by a live cable at a site.'; adv='Power isolated, casualty conscious, burns to the hand.'; line='They have killed the power and they are conscious, with entry and exit burns on the one hand.'; patient='a_m_y_construct_01'; seconds=15; pline='My hand is buzzing. It will not stop buzzing.' }
Add @{ id='ems-birth'; for='ems'; name='Birth Imminent'; msg='Woman in labour, no time for the hospital.'; adv='Conscious, partner present, contractions close.'; line='There is no time for the hospital and she knows it. You are the closest thing to a professional.'; patient='a_f_y_bevhills_01'; seconds=20; pline='Is she all right? Tell me she is all right.' }
Add @{ id='ems-withdrawal'; for='ems'; name='Withdrawal'; msg='Person shaking and vomiting, withdrawing from something.'; adv='Conscious, no injuries, refuses to say what.'; line='Shaking, sick, and not saying what they have been taking. Keep your kit close.'; patient='a_m_m_tramp_01'; seconds=14; pline='I just need to get through it. I have done it before.' }
Add @{ id='ems-psychiatric'; for='ems'; name='Psychiatric Emergency'; msg='Person in acute distress, family asking for help.'; adv='No weapon, no aggression, family on scene.'; line='Nobody has been hurt and nobody is threatening anybody. They are frightened and so is their family.'; res='Manual'; amb=$true }
Add @{ id='ems-glass-assault'; for='ems'; name='Assault with a Glass'; msg='Person cut in a bar, injury to the neck.'; adv='Conscious, bleeding heavily, suspect gone.'; line='Cut across the neck with a glass and the suspect has gone. Pressure on it and get them out of there.'; patient='a_m_y_business_01'; seconds=18; pline='It is not as bad as it looks. Is it as bad as it looks?' }
Add @{ id='ems-gunshot-wound'; for='ems,lspd'; name='Gunshot Wound'; msg='Person shot, conscious, suspect gone.'; adv='Wound to the shoulder, caller applying pressure.'; line='They have been shot in the shoulder and the caller is holding it. Suspect has gone - the patient comes first.'; patient='a_m_m_hasjew_01'; seconds=18; pline='I cannot move my arm. Tell me it is still there.' }
Add @{ id='ems-stab-wound'; for='ems,lspd'; name='Stab Wound'; msg='Person stabbed, wound to the abdomen.'; adv='Conscious, suspect gone, caller on scene.'; line='Stabbed in the stomach and still talking, which is not the same as being all right.'; patient='a_m_y_vinewood_01'; seconds=18; pline='It does not hurt as much as it should. Is that bad?' }
Add @{ id='ems-carbon-monoxide'; for='ems,fire'; name='Possible Carbon Monoxide'; msg='Family feeling unwell in a house, boiler suspected.'; adv='Four people, all drowsy and headachy.'; line='Four of them, all drowsy, all with headaches, and all in the same house. Get them out first.'; res='Manual'; amb=$true }
Add @{ id='ems-farm-machinery'; for='ems,sheriff'; name='Farm Machinery Injury'; msg='Person injured by a tractor, arm trapped.'; adv='Conscious, arm trapped, machine switched off.'; line='Their arm is in it and the machine is off, and now it is a question of getting them free without making it worse.'; patient='a_m_m_farmer_01'; seconds=20; pline='Do not move it. Whatever you do, do not move it.' }
Add @{ id='ems-sports-injury'; for='ems'; name='Sports Injury'; msg='Player injured on a pitch, suspected leg fracture.'; adv='Conscious, leg deformed, coaches keeping people back.'; line='The leg is not straight and they are being very brave about it. Splint them and keep the crowd off.'; patient='a_m_y_musclbeac_01'; seconds=14; pline='I heard it crack. I am not walking off.' }
Add @{ id='ems-food-poisoning'; for='ems'; name='Multiple People Unwell'; msg='Several customers unwell at a restaurant.'; adv='Four people, all vomiting, same meal.'; line='Four of them, all the same meal, and the kitchen is still serving. Start with the worst.'; res='Manual'; amb=$true }

# ============================================================ More again
#
# A third pass, for width rather than depth: the ordinary calls a shift is actually made of, so that a
# long patrol keeps producing something new.

# --- LSPD
Add @{ id='lspd-bag-snatch'; for='lspd'; name='Bag Snatch'; msg='Handbag snatched from a pedestrian.'; adv='One suspect on a scooter, bag taken, victim unhurt.'; line='They took it off her on a scooter and rode off. She is not hurt, which is luck more than anything.'; app='FleeInVehicle'; vehicle='faggio' }
Add @{ id='lspd-phone-snatch'; for='lspd'; name='Phone Snatch'; msg='Phone taken from a caller''s hand in the street.'; adv='Suspect on foot, victim chasing, no weapon seen.'; line='They took the phone out of her hand and ran. She is chasing them, which is not what I would advise.'; app='Flee' }
Add @{ id='lspd-prowler'; for='lspd'; name='Prowler Reported'; msg='Person trying door handles along a street.'; adv='One suspect on foot, no weapon seen, no entry made yet.'; line='They are going along the row trying every handle. Nobody has caught them at it.'; app='Flee' }
Add @{ id='lspd-silent-911'; for='lspd'; name='Silent 911 Call'; msg='Open line to 911, nothing said, address confirmed.'; adv='Line still open, background noise only.'; line='The line is open and nobody is talking. Could be a mistake, could be the opposite.'; res='Manual'; backup=$true }
Add @{ id='lspd-abandoned-call'; for='lspd'; name='Abandoned Call'; msg='Caller rang 911 and hung up.'; adv='Number rings out on callback, address known.'; line='They rang and hung up and they are not answering now. Somebody has to knock.'; res='Manual' }
Add @{ id='lspd-threatening-calls'; for='lspd'; name='Threatening Calls'; msg='Business receiving threatening phone calls.'; adv='No physical threat, calls logged, staff shaken.'; line='Nothing physical, but the calls are getting worse and the staff are frightened.'; res='Manual' }
Add @{ id='lspd-shots-into-building'; for='lspd'; name='Shots into a Building'; msg='Shots fired at a building, windows broken.'; adv='No casualties, vehicle seen leaving.'; line='Windows are out and no casualties, and a car left fast. That is your starting point.'; app='FleeInVehicle'; vehicle='premier'; amb=$true }
Add @{ id='lspd-fuel-driveoff'; for='lspd'; name='Fuel Drive-Off'; msg='Vehicle left a filling station without paying.'; adv='Plates noted, driver headed for the freeway.'; line='Filled it and left. Plates are with the station and they would like a word.'; app='FleeInVehicle'; vehicle='asea' }
Add @{ id='lspd-dine-and-dash'; for='lspd'; name='Dine and Dash'; msg='Group left a restaurant without paying.'; adv='Three people, staff have the description.'; line='Three of them, ate and walked out. Staff want it dealt with and they have the bill ready.'; app='Flee'; count=3 }
Add @{ id='lspd-hotel-disturbance'; for='lspd'; name='Hotel Disturbance'; msg='Guests fighting in a hotel corridor.'; adv='Two involved, staff asking for attendance.'; line='Two guests in the corridor and reception cannot get them to their rooms.'; app='Hostile'; count=2 }
Add @{ id='lspd-nightclub-ejection'; for='lspd'; name='Ejection from a Club'; msg='Person refusing to leave a nightclub.'; adv='Door staff holding them at the entrance.'; line='The door staff have them outside and they will not walk away. They are not armed, they are just determined.'; app='Hostile' }
Add @{ id='lspd-hospital-disturbance'; for='lspd'; name='Disorder at a Hospital'; msg='Person abusing staff in an emergency department.'; adv='One suspect, no weapon, staff want them removed.'; line='They are shouting at people who are trying to work. No weapon, but it is an emergency department.'; app='Hostile' }
Add @{ id='lspd-takeaway-robbery'; for='lspd'; name='Armed Robbery at a Takeaway'; msg='Armed robbery at a takeaway, staff locked in the back.'; adv='One suspect, weapon seen, staff unhurt.'; line='One suspect with a weapon and the staff are in the back room with the door shut.'; app='Flee'; armed=$true; count=1 }
Add @{ id='lspd-pawn-shop'; for='lspd'; name='Suspicious Pawn Shop Activity'; msg='Pawn shop reporting somebody trying to sell stock from a recent robbery.'; adv='Person at the counter, property matching a report.'; line='They are trying to sell something that matches a robbery from last week. The staff are keeping them talking.'; app='Flee' }
Add @{ id='lspd-scrapyard'; for='lspd'; name='Suspicious Scrapyard Load'; msg='Scrap yard reporting a vehicle arriving for crushing.'; adv='Vehicle matches a stolen report, driver on site.'; line='Somebody has brought in a car that matches a stolen report and wants it gone by this afternoon.'; res='Manual'; backup=$true }
Add @{ id='lspd-office-burglary'; for='lspd'; name='Office Burglary'; msg='Office broken into overnight, alarm activated late.'; adv='No suspects on scene, window forced.'; line='Window in and nobody about - they took their time and they took the laptops.'; res='Manual' }
Add @{ id='lspd-warehouse-burglary'; for='lspd'; name='Warehouse Break-In'; msg='Warehouse shutter forced, security reporting movement inside.'; adv='One person believed inside, no vehicle seen.'; line='Camera shows somebody inside and the shutter has been forced. They are still in there.'; app='Flee'; backup=$true }
Add @{ id='lspd-garage-burglary'; for='lspd'; name='Garage Break-In'; msg='Residential garage broken into, bicycles taken.'; adv='Shed and garage both forced, owner at work.'; line='Both doors forced and the bikes are gone. Owner is on their way home.'; res='Manual' }
Add @{ sex='female'; id='lspd-charity-theft'; for='lspd'; name='Theft from a Charity Shop'; msg='Someone has taken the collection tin from a charity shop.'; adv='Staff upset, description of one female.'; line='Somebody took the tin off the counter. The staff are more upset than they are angry.'; app='Flee' }
Add @{ id='lspd-museum-vandalism'; for='lspd'; name='Vandalism at a Museum'; msg='Display case damaged, staff reporting a suspect nearby.'; adv='One suspect, no weapon, no items taken.'; line='They put a display case through and walked off down the road. Nobody has taken anything.'; app='Flee' }
Add @{ id='lspd-grow-house'; for='lspd'; name='Suspected Grow House'; msg='Neighbours reporting heat and a hum from a rented house.'; adv='Nobody seen entering for weeks, windows covered.'; line='Windows covered, humming all night, and nobody has been in or out in weeks.'; res='Manual'; backup=$true }
Add @{ id='lspd-drug-house'; for='lspd'; name='Suspected Drug House'; msg='High foot traffic through a house at all hours.'; adv='Multiple visitors, brief stays, neighbours keep reporting it.'; line='Same house, same thing, four times this month. The neighbours have had enough.'; res='Manual'; backup=$true }
Add @{ id='lspd-shot-spotter'; for='lspd'; name='Shot Spotter Activation'; msg='Gunshot detection system has picked up shots.'; adv='No caller, no victim reported, exact address given.'; line='The system heard it and nobody has called it in. Go and have a look.'; res='Manual'; armed=$false }
Add @{ id='lspd-foot-pursuit-backup'; for='lspd'; name='Foot Pursuit Backup'; msg='Officer in a foot pursuit, requesting assistance.'; adv='Suspect running, officer behind, no weapon seen.'; line='They are on foot with the officer right behind them. Get into the next street.'; res='Manual'; backup=$true }
Add @{ id='lspd-pursuit-backup'; for='lspd,sahp'; name='Pursuit Assistance'; msg='Pursuit passing through, units requested to join.'; adv='Pursuit heading north, two units involved.'; line='They are coming through your area now. Join in behind, do not try to head it off.'; res='Manual'; backup=$true }
Add @{ id='lspd-bail-check'; for='lspd'; name='Bail Condition Check'; msg='Report that a person is breaching bail conditions.'; adv='Person seen at an address they should not be at.'; line='They are back at the address the bail conditions keep them away from. The complainant is watching them now.'; res='Manual' }
Add @{ id='lspd-curfew'; for='lspd'; name='Curfew Breach'; msg='Young person out after a court curfew.'; adv='Two of them, no offences committed, walking home.'; line='Two of them out after curfew and no offence committed. It is a conversation, not an arrest.'; res='Manual' }
Add @{ id='lspd-truancy'; for='lspd'; name='Reported Truancy'; msg='School reporting pupils off site during hours.'; adv='Two pupils in a park, no offence reported.'; line='Two of them in the park when they should be in a lesson. The school wants them brought back.'; res='Manual' }
Add @{ id='lspd-aggressive-begging'; for='lspd'; name='Aggressive Begging'; msg='Person begging aggressively outside a bank.'; adv='Suspect on foot, following customers, no weapon.'; line='They are following people to the cash machine. Nobody has been hurt, everybody is uncomfortable.'; res='Manual' }
Add @{ id='lspd-rough-sleeper'; for='lspd'; name='Rough Sleeper Concern'; msg='Person sleeping in a shop doorway in freezing weather.'; adv='Suspect, no visible injuries, business complaining.'; line='They are in the doorway and it is below freezing. Whatever the business wants, check on them first.'; res='Manual'; amb=$true }
Add @{ id='lspd-unlicensed-cab'; for='lspd'; name='Unlicensed Cab'; msg='Unlicensed driver picking up at a taxi rank.'; adv='One vehicle, driver arguing with licensed drivers.'; line='They are taking fares off the rank and the licensed drivers have told them twice. It is getting loud.'; res='Manual' }
Add @{ id='lspd-obstruction'; for='lspd'; name='Obstruction of the Highway'; msg='Vehicle parked on double yellows blocking a bus route.'; adv='No driver present, buses diverted.'; line='The buses cannot get through and the driver is nowhere. It needs moving.'; res='Manual' }
Add @{ id='lspd-return-domestic'; for='lspd'; name='Return to a Domestic Address'; msg='Neighbours reporting shouting again at an address attended last night.'; adv='Same address as last night, no weapons reported.'; line='Same address as last night, same shouting. Somebody has to go back.'; app='Hostile'; amb=$true }
Add @{ id='lspd-shoplifter-detained'; for='lspd'; name='Detained Shoplifter'; msg='Store staff holding a shoplifter in the office.'; adv='Suspect detained, cooperative, goods recovered.'; line='They have them in the office and the goods are on the desk. They are not fighting anybody.'; actors=@(@{models=(($peopleCity + $womenCity) -join ','); role='Suspect'; cower=$true}) }
Add @{ id='lspd-bike-theft-crew'; for='lspd'; name='Bike Theft in Progress'; msg='People cutting locks off bicycles at a rack.'; adv='Two of them with bolt croppers, several bikes.'; line='Two of them at the rack with croppers and they have already had two.'; app='Flee'; count=2 }
Add @{ id='lspd-van-theft'; for='lspd'; name='Theft from a Van'; msg='Tools being taken from a parked van.'; adv='One suspect, van broken into, tools on the pavement.'; line='They are unloading a van that is not theirs. The owner is a builder who is going to notice at seven.'; app='Flee' }
Add @{ id='lspd-parcel-theft'; for='lspd'; name='Parcel Theft'; msg='Person taking deliveries from doorsteps.'; adv='One suspect on foot, following a delivery van.'; line='They are walking the same street as the van and picking up what it leaves.'; app='Flee' }
Add @{ id='lspd-fare-dispute'; for='lspd'; name='Taxi Fare Dispute'; msg='Passenger refusing to pay a taxi fare.'; adv='Both on the pavement, no violence.'; line='Nobody is hurt, nobody is armed, and they both want you to decide who is right.'; res='Manual' }
Add @{ id='lspd-pool-hall-fight'; for='lspd'; name='Fight in a Pool Hall'; msg='Fight in a pool hall, one person with a cue.'; adv='Four involved, no firearms seen.'; line='Four of them and one has a cue. Get in there before it becomes something else.'; app='Hostile'; count=4; amb=$true }
Add @{ id='lspd-beach-disorder'; for='lspd'; name='Disorder on the Beach'; msg='Large group drinking and fighting on the beach.'; adv='Around ten people, no weapons seen, families complaining.'; line='Ten of them, drinking, and families are packing up to leave. Break it up.'; app='Hostile'; count=5; backup=$true }
Add @{ id='lspd-stadium-disorder'; for='lspd'; name='Disorder After a Match'; msg='Crowd disorder outside a stadium after a match.'; adv='Both sets of supporters, bottles thrown, no injuries yet.'; line='Both sets of them, bottles going, and the stewards are outnumbered. Back-up is coming.'; app='Hostile'; count=5; backup=$true; amb=$true }
Add @{ id='lspd-cash-machine-damage'; for='lspd'; name='ATM Damage'; msg='Cash machine attacked with tools overnight.'; adv='No suspects on scene, damage extensive.'; line='They went at it with something heavy and got nowhere. Forensics will want a look.'; res='Manual' }
Add @{ id='lspd-street-dispute'; for='lspd'; name='Neighbour Dispute'; msg='Long-running dispute between neighbours, shouting in the street.'; adv='Both parties outside, no violence.'; line='Same two, same argument, third time this month. Nobody has hit anybody yet.'; res='Manual' }
Add @{ id='lspd-car-alarm'; for='lspd'; name='Car Alarm Nuisance'; msg='Car alarm going off for hours, neighbours complaining.'; adv='No persons near the vehicle, alarm faulty.'; line='It has been going two hours and the owner is away. Everybody on the street is awake.'; res='Manual' }
Add @{ id='lspd-illegal-parked-taxi'; for='lspd'; name='Taxi Rank Dispute'; msg='Taxis blocking a rank and arguing with each other.'; adv='Three vehicles, drivers out, no violence.'; line='Three of them out of their cars shouting at each other over a fare.'; res='Manual' }
Add @{ id='lspd-suspicious-drone'; for='lspd'; name='Drone Over Houses'; msg='Drone flying low over gardens.'; adv='Operator seen on a nearby roof, no offence reported.'; line='Low over the gardens, and the operator is up on a roof. Somebody has complained about that specific drone three times.'; app='Flee' }
Add @{ id='lspd-found-weapon'; for='lspd'; name='Weapon Found'; msg='Knife handed in at a police station front desk.'; adv='No offences, item to be disposed of.'; line='Somebody has handed in a knife they found in their garden. It is paperwork, not a call, but it needs logging.'; res='Manual' }
Add @{ id='lspd-found-property'; for='lspd'; name='Property Handed In'; msg='Wallet handed in with identification inside.'; adv='Owner identifiable, no offence.'; line='It is a wallet with a name in it and no crime attached. If you have ten minutes, the owner lives round the corner.'; res='Manual' }

# --- Sheriff
Add @{ id='lssd-rustling'; for='sheriff'; name='Cattle Theft in Progress'; msg='Cattle being loaded at night onto a trailer.'; adv='Two suspects, trailer backed up to the gate, no weapons seen.'; line='They are loading cattle that are not theirs into a trailer in the dark. That is not a mistake.'; app='Flee'; armed=$true; count=2 }
Add @{ id='lssd-fence-cut'; for='sheriff'; name='Fence Cut on Farmland'; msg='Boundary fence cut, stock wandering onto the road.'; adv='No persons present, several animals loose.'; line='The fence is cut and half the field is on the road. Somebody did that deliberately.'; res='Manual' }
Add @{ id='lssd-stolen-quad'; for='sheriff'; name='Stolen Quad Bike'; msg='Quad bike taken from a farm yard.'; adv='Taken overnight, tracks heading for the trails.'; line='They wheeled it out in the night and it is on the trails somewhere.'; app='FleeInVehicle'; vehicle='blazer' }
Add @{ id='lssd-diesel-theft'; for='sheriff'; name='Diesel Stolen from a Tank'; msg='Fuel tank drained on a farm overnight.'; adv='No suspects, siphon marks at the tank.'; line='They have had the whole tank away. Owner is more annoyed than anything.'; res='Manual' }
Add @{ id='lssd-shots-at-farm'; for='sheriff'; name='Shots at a Farmhouse'; msg='Gunshots heard, farmhouse window broken.'; adv='Family inside and safe, no injuries.'; line='They put a shot through a window with the family inside. That is not poaching.'; app='HandsUp'; armed=$true; backup=$true }
Add @{ id='lssd-poacher-vehicle'; for='sheriff'; name='Poachers'' Vehicle Found'; msg='Vehicle left abandoned on a farm track.'; adv='No persons, lamping equipment inside.'; line='Nobody with it, but there is lamping kit on the back seat. They will come back for it.'; res='Manual'; backup=$true }
Add @{ id='lssd-distillery'; for='sheriff'; name='Distillery Reported'; msg='Copper tubing and drums seen on a hillside.'; adv='No persons present, set-up unattended.'; line='Tubing and drums and nobody about. Somebody has gone to a lot of trouble out here.'; res='Manual'; backup=$true }
Add @{ id='lssd-horse-theft'; for='sheriff'; name='Horse Taken from a Field'; msg='Horse missing from a field, gate left open.'; adv='No suspects, trailer tracks in the gateway.'; line='They walked it out to a trailer. The owner is distraught.'; res='Manual' }
Add @{ id='lssd-bull-escape'; for='sheriff'; name='Bull Loose'; msg='Bull escaped into a public footpath.'; adv='Animal agitated, walkers in the area.'; line='It is on the footpath and there are walkers up there. Keep everybody back and wait for the owner.'; res='Manual' }
Add @{ id='lssd-wild-dogs'; for='sheriff'; name='Pack of Wild Dogs'; msg='Pack of dogs worrying sheep on two farms.'; adv='Four or five animals, no owner identified.'; line='Four or five of them working two farms in a week. The farmers are taking it badly.'; res='Manual' }
Add @{ id='lssd-barn-break-in'; for='sheriff'; name='Barn Broken Into'; msg='Feed store and tools taken from a barn.'; adv='No suspects, door forced.'; line='Door forced and they cleared the tools out in the night. Nobody heard a thing.'; res='Manual' }
Add @{ id='lssd-body-in-river'; for='sheriff'; name='Body Found in the River'; msg='Body found by a fisherman in the river.'; adv='Deceased, no identification, scene untouched.'; line='A fisherman has found somebody in the water. Do not let anybody near it until you have been there.'; res='Manual'; amb=$true }
Add @{ id='lssd-county-drink-driver'; for='sheriff,sahp'; name='Drink Driver in the County'; msg='Vehicle in a ditch, driver still at the wheel.'; adv='Driver out and unsteady, no other vehicles involved.'; line='They are in the ditch and they are already telling the caller they are fine. They are not fine.'; amb=$true }
Add @{ id='lssd-trail-damage'; for='sheriff,ranger'; name='Damage to a Trail'; msg='Trail damaged by vehicles overnight.'; adv='Deep tyre marks, no vehicles present.'; line='Somebody has cut the trail up and left. The rangers will want to see it.'; res='Manual' }
Add @{ id='lssd-night-hunting'; for='sheriff'; name='Hunting After Dark'; msg='Lamping reported in a field after dark.'; adv='Two people on foot, rifles believed.'; line='They are lamping a field in the dark. Assume rifles and do not walk straight at them.'; app='Flee'; armed=$true; weapon='WEAPON_RIFLE'; count=2 }
Add @{ id='lssd-stolen-trailer'; for='sheriff'; name='Trailer Stolen'; msg='Livestock trailer taken from a yard.'; adv='Taken overnight, gate left open.'; line='They took the trailer and left the gate wide open. It will be on the highway somewhere.'; app='FleeInVehicle'; vehicle='trailers' }
Add @{ id='lssd-desert-body'; for='sheriff'; name='Body in the Desert'; msg='Body found off a dirt road.'; adv='Deceased, no identification, exposure likely.'; line='Somebody has been out here a while. Nobody has touched anything.'; res='Manual'; amb=$true }
Add @{ id='lssd-small-town-robbery'; for='sheriff'; name='Robbery in a Small Town'; msg='Hold-up at a gas station in a small town.'; adv='One suspect, cash taken, clerk unhurt.'; line='They took the till and walked out. Clerk is shaken and the town has two roads out, so pick one.'; app='FleeInVehicle'; vehicle='asea'; armed=$true }
Add @{ id='lssd-cattle-on-road'; for='sheriff'; name='Cattle on the Road'; msg='Cattle loose on a county road at night.'; adv='No persons present, several animals.'; line='They are on the road in the dark and drivers will not see them. That is the emergency.'; res='Manual' }
Add @{ id='lssd-farmhouse-alarm'; for='sheriff'; name='Farmhouse Alarm'; msg='Alarm at a remote farmhouse, keyholder an hour away.'; adv='No persons seen, driveway gates open.'; line='The gates are open and the keyholder is an hour out. Approach like somebody is still inside.'; res='Manual'; backup=$true }
Add @{ id='lssd-quarry-trespass'; for='sheriff'; name='Trespass at a Quarry'; msg='People on a disused quarry site.'; adv='Three or four on foot, no vehicles.'; line='Four of them in a place they have no business being. Nobody is hurt, which is not guaranteed there.'; res='Manual' }

# --- Highway Patrol
Add @{ id='sahp-speeding-stop-backup'; for='sahp'; name='Speeding Stop Backup'; msg='Officer on a stop requesting a second unit.'; adv='Driver argumentative, single officer on scene.'; line='They are arguing with one officer on the hard shoulder and it is getting louder.'; res='Manual'; backup=$true }
Add @{ id='sahp-motorcycle-collision'; for='sahp,ems'; name='Motorcycle Collision'; msg='Motorcyclist down, driver of the other vehicle on scene.'; adv='Rider conscious, leg injured, lane blocked.'; line='Rider is down and conscious, and the car driver is standing there not knowing what to do.'; patient='a_m_y_biker_01'; seconds=16; pline='I am all right. I am all right. Is the bike all right?' }
Add @{ id='sahp-truck-collision'; for='sahp'; name='Lorry Collision'; msg='Two lorries in collision, carriageway blocked.'; adv='Both drivers out and walking, load spilled.'; line='Both drivers are out and walking, which is remarkable given the state of the cabs.'; res='Manual'; amb=$true }
Add @{ id='sahp-bus-collision'; for='sahp,ems'; name='Bus in Collision'; msg='Bus in collision with a car, passengers on board.'; adv='Passengers shaken, one with a head injury.'; line='There are passengers on it and one of them has taken a knock to the head. The driver is not hurt.'; patient='a_m_m_business_01'; seconds=14; pline='I hit my head on the handrail. I was standing up.' }
Add @{ id='sahp-wrong-way-motorway'; for='sahp'; name='Vehicle on the Wrong Side'; msg='Vehicle travelling the wrong way on the freeway.'; adv='Multiple callers, headlights on, no collision yet.'; line='Several callers and it is still going. Get ahead of it if you can.'; app='FleeInVehicle'; vehicle='premier' }
Add @{ id='sahp-breakdown-night'; for='sahp'; name='Broken Down in the Dark'; msg='Vehicle stopped in a live lane with no lights.'; adv='No driver visible, traffic swerving.'; line='It is in a lane with no lights on it and the traffic is swerving. That is the whole call.'; res='Manual' }
Add @{ id='sahp-overheight'; for='sahp'; name='Overheight Vehicle'; msg='Vehicle has struck a bridge and is stuck.'; adv='No injuries, load wedged, lane closed.'; line='They have taken the bridge on with the load and lost. Nobody is hurt, the lane is shut.'; res='Manual' }
Add @{ id='sahp-overloaded-pickup'; for='sahp'; name='Overloaded Pickup'; msg='Pickup carrying a dangerous load on the freeway.'; adv='Load unsecured, driver unaware of the problem.'; line='Whatever is on the back of that is one pothole away from the windscreen behind it.'; app='FleeInVehicle'; vehicle='bobcatxl' }
Add @{ id='sahp-stolen-plate'; for='sahp'; name='Stolen Plates Reported'; msg='Plates reported stolen from a parked car.'; adv='No suspects, vehicle still parked.'; line='Somebody has had their plates off in a car park. They will be on something soon.'; res='Manual' }
Add @{ id='sahp-fuel-spill'; for='sahp,fire'; name='Diesel Spill'; msg='Fuel spill across two lanes after a collision.'; adv='No injuries, spill spreading, traffic still moving.'; line='Diesel across two lanes and traffic driving through it. Somebody is going to slide.'; res='Manual' }
Add @{ id='sahp-tunnel-breakdown'; for='sahp'; name='Breakdown in a Tunnel'; msg='Vehicle stopped inside a tunnel, smoke reported.'; adv='Driver out of the vehicle, tunnel filling with fumes.'; line='They are out of it and the tunnel is filling up with what is coming off it. Get the traffic stopped.'; res='Manual'; amb=$true }
Add @{ id='sahp-bridge-stopped'; for='sahp'; name='Vehicle Stopped on a Bridge'; msg='Vehicle parked on a bridge, driver seen at the rail.'; adv='No other parties, caller concerned.'; line='They are out of the car and they are at the rail. Do not rush this one.'; res='Manual'; amb=$true }
Add @{ id='sahp-abnormal-load'; for='sahp'; name='Abnormal Load Escort'; msg='Escort requested for an abnormal load.'; adv='Load waiting, route surveyed, timings agreed.'; line='They cannot move until somebody is in front of it. It is a slow one.'; res='Manual' }

# --- Rangers
Add @{ id='sapr-dumped-chemicals'; for='ranger'; name='Dumped Chemicals'; msg='Drums dumped at a beauty spot.'; adv='No persons present, containers leaking.'; line='Drums tipped over the edge and something coming out of them. Do not touch them.'; res='Manual'; backup=$true }
Add @{ id='sapr-injured-horse'; for='ranger'; name='Injured Horse on a Trail'; msg='Horse down on a trail, rider with it.'; adv='Rider unhurt, animal unable to stand.'; line='The horse is down and the rider is sitting with it. Neither of them is going anywhere fast.'; res='Manual'; amb=$true }
Add @{ id='sapr-fire-lookout'; for='ranger,fire'; name='Fire Lookout Report'; msg='Smoke reported from a fire lookout tower.'; adv='Smoke to the east, no access road.'; line='The lookout has smoke to the east and no road within a mile of it. That is the start of something.'; app='Fire' }
Add @{ id='sapr-missing-swimmer'; for='ranger,ems'; name='Missing Swimmer'; msg='Swimmer not seen for thirty minutes at a lake.'; adv='Friends on the shore, no sighting.'; line='Thirty minutes and nothing. The friends are on the shore and they are not sure where they went in.'; res='Manual'; amb=$true }
Add @{ id='sapr-rockfall'; for='ranger'; name='Rockfall on a Trail'; msg='Rockfall has blocked a trail, walkers above it.'; adv='Walkers on the far side, no injuries.'; line='The trail is blocked and there are walkers on the wrong side of it.'; res='Manual' }
Add @{ id='sapr-drone-over-park'; for='ranger'; name='Drone Over Protected Land'; msg='Drone flown over a protected area.'; adv='Operator on a ridge, drone airborne.'; line='It is over ground it should not be, and the operator is up there flying it.'; app='Flee' }
Add @{ id='sapr-illegal-tour'; for='ranger'; name='Unlicensed Tour Group'; msg='Group being led through a protected area.'; adv='Guide with twelve people, no permit.'; line='Twelve of them following somebody with no permit. Nobody is in danger, it is just the wrong place for it.'; res='Manual' }
Add @{ id='sapr-litter-fire'; for='ranger,fire'; name='Fire in a Rubbish Bin'; msg='Bin fire at a picnic area.'; adv='Nobody about, fire spread to grass.'; line='Bin fire and it has taken the grass with it. Wind is doing you no favours.'; app='Fire' }

# --- Prison
Add @{ id='saspa-yard-fight'; for='prison'; name='Fight in the Yard'; msg='Fight between inmates in the exercise yard.'; adv='Several involved, staff responding, no weapons seen.'; line='Half a dozen of them at it in the yard and the staff are going in. Nobody has used a weapon yet.'; app='Hostile'; count=4; amb=$true }
Add @{ id='saspa-drug-find'; for='prison'; name='Drugs Found in a Cell'; msg='Search has found drugs in a cell, occupant identified.'; adv='Occupant cooperative, quantity small.'; line='It was under the mattress and they are not arguing about it. Paperwork rather than a scene.'; actorcower=$true }
Add @{ id='saspa-self-harm'; for='prison'; name='Self-Harm Concern'; msg='Inmate assessed as a risk, staff requesting attendance.'; adv='No injury yet, staff on the door.'; line='They have told staff what they intend to do and they believe them. Nobody is hurt yet.'; res='Manual'; amb=$true }
Add @{ id='saspa-perimeter-alarm'; for='prison'; name='Perimeter Alarm'; msg='Perimeter alarm triggered, no visual.'; adv='No sighting, fence intact.'; line='The alarm went and nobody has seen anybody. It could be wildlife, or it could not be.'; res='Manual'; backup=$true }
Add @{ id='saspa-visitor-argument'; for='prison'; name='Argument at Visits'; msg='Argument in the visits hall, staff asking for support.'; adv='Two visitors, no violence, staff holding them apart.'; line='Two of them over a table and the staff have them apart. It is loud, not violent.'; res='Manual' }
Add @{ id='saspa-transfer-escort'; for='prison,sheriff'; name='Transfer Escort Request'; msg='Escort requested for a transfer to court.'; adv='Inmate ready, vehicle waiting, route clear.'; line='They are ready and the vehicle is waiting. They will not move without a second unit.'; res='Manual' }

# --- EMS
Add @{ id='ems-choking'; for='ems'; name='Choking'; msg='Person choking, still conscious, airway partly clear.'; adv='Restaurant staff on scene, patient distressed.'; line='Still conscious and still choking. Whatever they ate, nobody has got it out yet.'; patient='a_m_y_business_02'; seconds=10; pline='(coughing) It is out. It is out.' }
Add @{ id='ems-head-injury'; for='ems'; name='Head Injury'; msg='Person fallen and struck their head.'; adv='Conscious but confused, bleeding from the scalp.'; line='Head wounds bleed like nothing else and confuse afterwards. Watch the confusion, not the blood.'; patient='a_m_y_bevhills_02'; seconds=15; pline='I do not remember falling. Did I fall?' }
Add @{ id='ems-anaphylaxis-child'; for='ems'; name='Child with a Severe Reaction'; msg='Child swollen after eating, parent panicking.'; adv='Conscious, breathing noisy, parent with a pen.'; line='A child, a noisy airway, and a parent who has never used the pen before. Take the lead.'; patient='a_m_y_hipster_01'; seconds=12; pline='They are breathing better. They are breathing.' }
Add @{ id='ems-elderly-confusion'; for='ems'; name='Sudden Confusion'; msg='Elderly person suddenly confused, carer concerned.'; adv='Conscious, no injury, normally lucid.'; line='They were fine this morning and now they do not know the carer. That is new, and new is the point.'; patient='a_m_m_tramp_01'; seconds=14; pline='Who are you? Where is my daughter?' }
Add @{ id='ems-dehydration'; for='ems'; name='Collapse After Exercise'; msg='Runner collapsed at the end of a race.'; adv='Conscious, hot and dry, event medics on scene.'; line='Hot, dry and confused at the end of a race. Get fluids into them before anything else.'; patient='a_m_y_musclbeac_01'; seconds=12; pline='I finished it. Did I finish it?' }
Add @{ id='ems-punch-injury'; for='ems'; name='Assault - Punch Injury'; msg='Person punched outside a bar, face bleeding.'; adv='Conscious, suspect gone, nose likely broken.'; line='One punch, one face, and the suspect is already down the road.'; patient='a_m_y_business_01'; seconds=12; pline='They just hit me. I did not say anything to them.' }
Add @{ id='ems-stairs-fall'; for='ems'; name='Fall Down Stairs'; msg='Person fallen down a flight of stairs at home.'; adv='Conscious, hip and wrist pain, family at home.'; line='Down the whole flight, and she is more worried about the mess than her wrist.'; patient='a_f_y_bevhills_02'; seconds=14; pline='I missed the last step. It hurt more than I expected.' }
Add @{ id='ems-sepsis'; for='ems'; name='Suspected Sepsis'; msg='Person unwell with a high temperature and confusion.'; adv='Conscious, hot to touch, family reporting a recent infection.'; line='Hot, confused, and there was an infection last week. That set of things together is not ordinary.'; patient='a_f_y_business_02'; seconds=15; pline='I am so cold. Why am I so cold?' }
Add @{ id='ems-dialysis-missed'; for='ems'; name='Missed Dialysis'; msg='Person unwell after missing a dialysis appointment.'; adv='Conscious, swollen, hospital trying to contact them.'; line='They have missed two appointments and their body is telling everybody about it.'; patient='a_m_m_business_01'; seconds=14; pline='I could not get there. I could not get there.' }
Add @{ id='ems-overdose-teen'; for='ems'; name='Overdose - Young Person'; msg='Young person unresponsive at a party.'; adv='Breathing shallow, friends present and frightened.'; line='Shallow breathing and a room full of frightened teenagers. Get to her and keep the questions for later.'; patient='a_f_y_hipster_01'; seconds=12; pline='I did not think it would do that. Is she awake?' }
Add @{ id='ems-chemical-inhalation'; for='ems,fire'; name='Chemical Inhalation'; msg='Worker breathing fumes in a workshop.'; adv='Conscious, coughing, area cleared.'; line='They are coughing and there is nothing in the air now, which does not mean it has left their lungs.'; patient='a_m_y_construct_02'; seconds=16; pline='It went in my chest and stayed there.' }
Add @{ id='ems-forklift'; for='ems'; name='Forklift Injury'; msg='Worker struck by a forklift in a yard.'; adv='Conscious, leg crushed, driver shocked but helping.'; line='Leg under the machine and they are conscious. This is a get-them-to-hospital call, not a treat-them-here one.'; patient='a_m_y_construct_01'; seconds=18; pline='Do not look at it. Just do not look at it.' }
Add @{ id='ems-multiple-stabbing'; for='ems,lspd'; name='Multiple Casualties - Stabbing'; msg='Two people stabbed, one seriously.'; adv='Both conscious, suspect gone, blood on the pavement.'; line='Two of them and one is much worse than the other. Start with the quiet one.'; patient='a_m_m_hasjew_01'; seconds=18; pline='They got me in the side. I cannot stand up.' }
Add @{ id='ems-multiple-shooting'; for='ems,lspd'; name='Multiple Casualties - Shots'; msg='Two people shot in the street.'; adv='One walking wounded, one on the ground.'; line='One walking and one not. The walking one is not the priority, whatever they say.'; patient='a_m_m_business_01'; seconds=18; pline='I am fine. Help them first. Help them.' }
Add @{ id='ems-child-drowning'; for='ems,ranger'; name='Child in the Water'; msg='Child pulled from a pool, not breathing.'; adv='Parent doing compressions, water coming out.'; line='A parent is doing compressions on their own child. Get there and take over.'; patient='a_f_y_beach_01'; seconds=20; pline='(a wet cough, then crying)' }
Add @{ id='ems-sport-head'; for='ems'; name='Head Injury at a Game'; msg='Player knocked out briefly on the pitch.'; adv='Conscious, confused, coaches holding them down.'; line='They were out for a second and now they think they are fine. They are not going back on that pitch.'; patient='a_m_y_musclbeac_02'; seconds=14; pline='What was the score? Did we win?' }
Add @{ id='ems-machinery-shear'; for='ems,fire'; name='Trapped in Machinery'; msg='Person trapped in machinery at a depot.'; adv='Conscious, arm trapped, power off, staff with them.'; line='Trapped and conscious, and the fire service are cutting them out. Keep them talking.'; patient='a_m_y_construct_02'; seconds=18; pline='I can hear you. I can hear you.' }
Add @{ id='ems-burns-face'; for='ems,fire'; name='Burns to the Face'; msg='Person with facial burns from a gas ring.'; adv='Conscious, burns to face and hands, eyes closed.'; line='Face and hands and their eyes are shut. Do not let them touch them.'; patient='a_m_m_business_02'; seconds=15; pline='I cannot open my eyes. Say something so I know where you are.' }

# --- Fire
Add @{ id='fire-house-occupied'; for='fire'; name='House Fire - People Inside'; msg='House fire with people reported inside.'; adv='Two residents unaccounted for, smoke from the upper floor.'; line='Two of them unaccounted for and smoke coming out of the top floor. Get there.'; app='Fire'; backup=$true; amb=$true }
Add @{ id='fire-flat-smoke'; for='fire'; name='Smoke in a Block of Flats'; msg='Smoke in a communal stairwell of a block.'; adv='Residents evacuating, source unknown.'; line='The stairwell is full of smoke and nobody knows which flat it is coming from.'; app='Fire'; amb=$true }
Add @{ id='fire-restaurant'; for='fire'; name='Restaurant Kitchen Fire'; msg='Kitchen fire in a restaurant, staff evacuated.'; adv='Extractor involved, staff outside, no injuries.'; line='It has gone up the extractor and the staff are outside. Nobody is hurt.'; app='Fire' }
Add @{ id='fire-factory'; for='fire'; name='Factory Fire'; msg='Fire at a factory unit, staff evacuating.'; adv='Rolling doors open, smoke across the yard.'; line='A working factory, staff coming out, and whatever is in there is burning hot.'; app='Fire'; backup=$true }
Add @{ id='fire-garage'; for='fire'; name='Garage Fire'; msg='Fire in a domestic garage with fuel inside.'; adv='Occupants out, fuel cans inside.'; line='There is fuel in there and everybody is out. Keep well back from the doors.'; app='Fire'; backup=$true }
Add @{ id='fire-car-row'; for='fire'; name='Row of Cars on Fire'; msg='Several vehicles alight in a car park.'; adv='Four or five vehicles, no persons at risk.'; line='Four or five of them and it is moving down the row. Water and distance.'; app='Fire' }
Add @{ id='fire-grass-hills'; for='fire'; name='Hillside Grass Fire'; msg='Grass fire spreading on a hillside above houses.'; adv='Wind towards the houses, no evacuation yet.'; line='It is going uphill towards the houses and the wind is helping it. That is the whole job.'; app='Fire'; backup=$true }
Add @{ id='fire-boat'; for='fire'; name='Boat Fire'; msg='Boat on fire at a marina.'; adv='Nobody on board, fire spreading to the pontoon.'; line='Nobody aboard and it is trying to take the pontoon with it.'; app='Fire' }
Add @{ id='fire-caravan'; for='fire'; name='Caravan Fire'; msg='Caravan alight on a site.'; adv='Occupants out, gas cylinder inside.'; line='They are out, and there is a gas bottle in it. Everybody back.'; app='Fire'; backup=$true }
Add @{ id='fire-warehouse'; for='fire'; name='Warehouse Fire'; msg='Fire in a storage warehouse, unknown contents.'; adv='Staff evacuated, pallets and chemicals inside.'; line='Nobody knows what is on those pallets, which is the part that matters.'; app='Fire'; backup=$true }
Add @{ id='fire-gas-leak'; for='fire'; name='Gas Leak Reported'; msg='Strong smell of gas in a residential street.'; adv='Residents outside, no ignition source yet.'; line='You can smell it from the end of the street. Nobody is to touch a doorbell.'; res='Manual'; backup=$true }
Add @{ id='fire-chimney'; for='fire'; name='Chimney Fire'; msg='Chimney fire in an old house, smoke from the roof.'; adv='Occupants out, fire contained so far.'; line='It is in the chimney for now and the householder is watching it. That is not a plan.'; app='Fire' }
Add @{ id='fire-electrical-depot'; for='fire'; name='Electrical Fire at a Depot'; msg='Fire in an electrical cabinet at a depot.'; adv='Power isolated by staff, no injuries.'; line='They have killed the power, so it is smoke and burnt plastic rather than anything worse.'; app='Fire' }
Add @{ id='fire-alley-rubbish'; for='fire'; name='Fire in an Alleyway'; msg='Rubbish alight in an alley between buildings.'; adv='No persons about, fence beginning to scorch.'; line='It is in the alley between the buildings and the fence is going to go first.'; app='Fire' }

# ============================================================ The deep ones
#
# Recipes with the two things a scene usually cannot have: people who answer when they are spoken to,
# and a sequence rather than a single reaction. These are the closest a recipe gets to the scripted
# set-pieces - they still build a scene rather than a cut scene, but the scene now talks back and moves
# on by itself.

Add @{ id='deep-hostage-standoff'; for='lspd'; name='Standoff - Suspect Inside'; msg='Domestic incident escalated, one person inside refusing to come out.'; adv='One suspect, no weapon confirmed, occupants out.'; line='They are in there on their own and they will not come out. Start talking - I am holding everybody back.'; app='none'; stages=@(@{at=20; do='backup'; text='Second unit is on the street behind you.'; }, @{at=50; do='handsup'; text='They have their hands on their head.'; }, @{at=80; do='end'; text='They are in the car. Call finished.'; }); brief='Nobody has been hurt yet and that is the whole job: talk them out.'; backup=$true }
Add @{ id='deep-bank-siege'; for='lspd'; name='Bank Siege'; msg='Armed robbery turned siege, staff inside with the suspects.'; adv='Two suspects, weapons seen, no shots fired.'; line='Two of them and staff in there. Do not go in - establish contact and keep them talking.'; app='none'; stages=@(@{at=15; do='backup'; text='SWAT is three minutes out.'; }, @{at=60; do='handsup'; text='The door is opening - hands first.'; }, @{at=100; do='end'; text='Both in custody. Tally the staff and let them go.'; }); brief='A siege is a conversation with a clock on it. Keep them talking.'; backup=$true }
Add @{ id='deep-medical-patient-talks'; for='ems'; name='Patient Who Can Talk'; msg='Person collapsed but conscious, wants to explain before you treat them.'; adv='Conscious, no obvious injury, refusing to be moved.'; line='She is awake and she wants to talk to you before anybody touches her.'; patient='a_f_y_business_02'; seconds=14; pline='It went tight and then it went numb. It is going again now.'; brief='She can talk, which is information. Listen before you treat.'; amb=$true }
Add @{ id='deep-gang-fight'; for='lspd'; name='Gang Fight in Progress'; msg='Two groups fighting, weapons mentioned by the caller.'; adv='Six or more, bottles and at least one knife.'; line='Six of them and a knife mentioned. Wait for your second unit - I am not sending you in alone.'; app='Hostile'; count=4; backup=$true; amb=$true; stages=@(@{at=15; do='backup'; text='Second unit is turning into the street.'; }, @{at=45; do='hostile'; text='Knife.'; }, @{at=90; do='end'; text='Two in custody. Ambulance for the one on the ground.'; }) }
# ============================================================ Federal - FIB
#
# The agencies that had nothing. These are the callouts for them: a warrant, a watch, an informant, a
# seizure. They lean on the two things the pack already does - a scene you go to and people in it who
# answer - rather than on anything federal that LSPDFR does not have.

Add @{ id='fib-surveillance-crew'; for='fib'; name='Surveillance - Suspected Robbery Crew'; msg='Request for surveillance of a crew believed to be planning a robbery.'; adv='Two vehicles, three suspects, no weapons seen.'; line='They have been sat on that corner an hour. Keep out of sight and keep the log.'; brief='Watch them. If they move on a target, that is when we go, and we go as one.'; app='none'; count=3; vehicle='asea'; stages=@(@{at=40; do='line'; text='One of them is out and walking the block.'; }, @{at=90; do='fleeinvehicle'; text='They are moving. Both vehicles, northbound.'; }, @{at=150; do='end'; text='Vehicle recovered. Two in custody, one on the floor.'; }) }
Add @{ id='fib-stash-house-raid'; for='fib'; name='Raid - Suspected Stash House'; msg='Warrant to be executed at an address holding stolen goods.'; adv='Occupants inside, possibly armed.'; line='The warrant is signed. Nobody moves until the team is at the door.'; brief='A signed warrant and a front door. Go in loud, go in together.'; app='none'; armed=$true; count=2; backup=$true; stages=@(@{at=20; do='line'; text='Team is at the door. Entry on your word.'; }, @{at=60; do='hostile'; text='One of them has gone for the back door.'; }, @{at=120; do='end'; text='Property recovered and the address is secure.'; }) }
Add @{ id='fib-informant-missed'; for='fib'; name='Informant - Missed Meeting'; msg='Informant has not made a scheduled meeting.'; adv='Meeting point is a car park, nobody answering the phone.'; line='The meeting was at eight and nobody has seen them. That is not like them.'; brief='An informant who does not turn up is either frightened or turned.'; app='none'; res='Manual' }
Add @{ id='fib-witness-address'; for='fib'; name='Protection - Witness Address'; msg='Threats made against a protected witness at a safe address.'; adv='Witness inside, no damage visible to the property.'; line='Somebody has been past the address twice this morning and the witness does not know.'; brief='Keep them alive and keep them calm, in that order.'; backup=$true }
Add @{ id='fib-counterfeit-workshop'; for='fib'; name='Counterfeit Currency - Workshop'; msg='Print workshop found in a unit on an industrial estate.'; adv='Presses running, nobody outside the unit.'; line='The presses are running and there is paper stacked to the roof. Nobody has come out.'; brief='A press that is running is evidence. Take the room before anybody clears it.'; backup=$true }
Add @{ id='fib-firearms-interdiction'; for='fib'; name='Firearms Interdiction'; msg='Vehicle believed to be carrying firearms into the city.'; adv='Two occupants, vehicle known to the agency.'; line='It came off the highway twenty minutes ago. Do not stop it in traffic - wait for the lay-by.'; brief='If it runs it runs, but the weapons go with it. Take the lay-by stop.'; app='FleeInVehicle'; armed=$true; vehicle='asea'; count=2 }
Add @{ id='fib-laundering-front'; for='fib'; name='Laundering - Front Business'; msg='Business believed to be laundering money through the till.'; adv='Staff inside, paperwork behind the counter.'; line='The takings do not match the trade and the paperwork is behind the counter. Go in and ask to see it.'; res='Manual' }
Add @{ id='fib-trafficking-lorry'; for='fib'; name='Trafficking - Lorry Intercept'; msg='Lorry believed to be moving people into the county.'; adv='Curtain-sided lorry, one driver, last seen on the highway.'; line='It is a curtain-sider and it has not stopped at the last two checks. Get it off the road.'; brief='There are people in the back of that lorry. Whatever happens, they are not the suspects.'; app='FleeInVehicle'; vehicle='phantom' }
Add @{ id='fib-kidnap-recovery'; for='fib'; name='Kidnap - Recovery Operation'; msg='Person taken from a street and believed held locally.'; adv='One vehicle involved, direction of travel known.'; line='The car went east and there is somebody in the back of it. This is a recovery, not a chase.'; app='FleeInVehicle'; armed=$true; vehicle='buffalo'; count=2; backup=$true }
Add @{ id='fib-fraud-server-unit'; for='fib'; name='Cyber Fraud - Server Address'; msg='Server address linked to a fraud network.'; adv='Unit in a business park, nobody outside.'; line='The address is a unit in a business park and it has been paid for a year in advance.'; backup=$true; res='Manual' }
Add @{ id='fib-organised-crime-meet'; for='fib'; name='Organised Crime - Meeting in Progress'; msg='Meeting of two groups at a rural property.'; adv='Six or more vehicles at the address.'; line='Six vehicles at a house that holds two people. Nobody meets in that car park by accident.'; app='none'; armed=$true; count=4; backup=$true; stages=@(@{at=30; do='line'; text='Two of them are at the gate, watching the road.'; }, @{at=70; do='flee'; text='They have seen the unit. They are going over the back fence.'; }, @{at=140; do='end'; text='Four detained. Two vehicles seized.'; }) }
Add @{ id='fib-property-recovery'; for='fib'; name='Stolen Property - Recovery'; msg='High-value property believed held in a garage.'; adv='Nobody visible, garage locked.'; line='The property went missing three weeks ago and it is in that garage. The rest is paperwork.'; res='Manual' }
Add @{ id='fib-weapons-cache'; for='fib'; name='Weapons Cache - Woodland'; msg='Cache of weapons reported in woodland.'; adv='Cache under a tarpaulin, no persons present.'; line='It is under a tarpaulin with a padlock on it, about forty metres off the track.'; backup=$true; res='Manual' }
Add @{ id='fib-narcotics-warehouse'; for='fib'; name='Narcotics - Warehouse Raid'; msg='Warrant for a warehouse believed to hold narcotics.'; adv='Shutter down, movement heard inside.'; line='The shutter is down, there is movement inside, and the warrant covers all of it.'; app='none'; armed=$true; count=3; backup=$true; stages=@(@{at=25; do='line'; text='The shutter is coming up on its own.'; }, @{at=55; do='hostile'; text='They are not coming out. They are going for the yard.'; }, @{at=130; do='end'; text='Yard secured and the seizure is starting.'; }) }
Add @{ id='fib-courier-intercept'; for='fib'; name='Courier Intercept'; msg='Courier believed to be carrying evidence out of the city.'; adv='One suspect on foot, bag over the shoulder.'; line='They are walking for the station with a bag they did not have this morning. Do not lose sight of the bag.'; brief='The bag is the evidence. The person is secondary.'; app='Flee' }
Add @{ id='fib-witness-threats'; for='fib'; name='Threats - Protected Witness'; msg='Threatening calls made to a protected address.'; adv='Caller using a payphone, witness at home.'; line='Two calls this week to a number nobody should have. The number is the lead.'; res='Manual' }
Add @{ id='fib-forgery-print-shop'; for='fib'; name='Document Forgery - Print Shop'; msg='Forged documents being produced at a print shop.'; adv='Shop open, two staff inside.'; line='The shop is open and the laminator is going. Ask to see the licence and watch the back room.'; res='Manual' }
Add @{ id='fib-coast-drop'; for='fib'; name='Contraband Drop - Coast Road'; msg='Contraband believed to be landing on the coast road.'; adv='Vehicle waiting on the verge with its lights off.'; line='A van with its lights off on the coast road at two in the morning. Nobody does that for the view.'; app='FleeInVehicle'; vehicle='burrito'; armed=$true }
Add @{ id='fib-protected-visit'; for='fib'; name='Threat Assessment - Protected Visit'; msg='Protected person arriving, route to be checked first.'; adv='No specific threat, the route has been used before.'; line='The route is straightforward, which is exactly why I want it walked before they move.'; res='Manual'; backup=$true }
Add @{ id='fib-phone-shop-front'; for='fib'; name='Front Business - Stolen Handsets'; msg='Shop believed to be re-selling stolen handsets.'; adv='Shop open, display cases full.'; line='Every handset in that window came from somewhere it should not have been. Ask for the books.'; res='Manual' }

# ============================================================ Federal - NOOSE

Add @{ id='noose-bomb-threat'; for='noose'; name='Bomb Threat - Public Building'; msg='Threat called in against a public building.'; adv='Building being evacuated by staff.'; line='The call came to the front desk and the building is coming out now. Nobody goes in.'; brief='A threat is not a device. Clear the building, hold the cordon, let the team work.'; app='none'; backup=$true; res='Manual'; stages=@(@{at=45; do='line'; text='Evacuation is complete and nobody is unaccounted for.'; }, @{at=120; do='end'; text='Search finished. Nothing found, building handed back.'; }) }
Add @{ id='noose-armed-barricade'; for='noose'; name='Armed Barricade - Federal Warrant'; msg='Armed suspect barricaded in a flat.'; adv='One inside, weapon seen at the window.'; line='They are in the flat with the door barricaded and something at the window. Nobody goes through that door yet.'; app='none'; armed=$true; backup=$true; stages=@(@{at=40; do='line'; text='Negotiators are on the phone with them.'; }, @{at=110; do='handsup'; text='The door is open and there are hands in the doorway.'; }, @{at=170; do='end'; text='In custody. Stand the cordon down.'; }) }
Add @{ id='noose-hostage-rescue'; for='noose'; name='Hostage Rescue - Staged Assault'; msg='Hostage held inside a business premises.'; adv='Two suspects and at least one hostage.'; line='Two of them, at least one hostage, and they have stopped answering the phone.'; app='none'; armed=$true; count=2; backup=$true; amb=$true; stages=@(@{at=35; do='line'; text='Entry teams are set on both doors.'; }, @{at=80; do='hostile'; text='They are at the back door with the hostage.'; }, @{at=150; do='end'; text='Hostage out and both suspects detained.'; }) }
Add @{ id='noose-explosives-cache'; for='noose'; name='Explosives Cache Found'; msg='Suspected explosives found in a lock-up.'; adv='Lock-up standing open, nobody on scene.'; line='It is standing open and there is no vehicle outside it. The team is twenty minutes behind me.'; res='Manual'; backup=$true }
Add @{ id='noose-major-incident'; for='noose'; name='Major Incident - Forward Command'; msg='Major incident declared, forward command point needed.'; adv='Several agencies on scene, no unified command.'; line='Everybody is shouting into a different radio. What is needed is one point and one voice.'; res='Manual'; backup=$true; amb=$true }
Add @{ id='noose-counter-terror-raid'; for='noose'; name='Counter-Terrorism Raid'; msg='Warrant to be executed at an address under investigation.'; adv='Occupants believed armed, property covered front and rear.'; line='Both doors are covered and the warrant is in my hand. Entry on your word.'; app='none'; armed=$true; count=2; backup=$true; stages=@(@{at=20; do='line'; text='Both doors covered. Entry when you are ready.'; }, @{at=65; do='hostile'; text='One has come out of the back with something in hand.'; }, @{at=140; do='end'; text='Address secure. Evidence teams going in.'; }) }
Add @{ id='noose-protected-escort'; for='noose'; name='Protected Escort - Route Threat'; msg='Escort required for a protected transfer.'; adv='Route is short, threat is general.'; line='Two vehicles, four minutes apart, and the route does not change once it starts.'; res='Manual'; backup=$true }
Add @{ id='noose-perimeter-breach'; for='noose'; name='Secure Site - Perimeter Breach'; msg='Perimeter alarm at a secure site, no visual.'; adv='Camera offline at the breach point.'; line='The alarm is on the north fence and the camera covering it has been down two days.'; backup=$true; app='Flee' }
Add @{ id='noose-rail-package'; for='noose'; name='Rail Incident - Suspicious Package'; msg='Suspicious package found at a station.'; adv='Trains held, platform cleared.'; line='The trains are held and the platform is clear. Nobody touches the bag.'; res='Manual'; backup=$true }
Add @{ id='noose-roof-assault'; for='noose'; name='Air Support - Roof Assault'; msg='Suspect on a roof, ground units unable to reach.'; adv='One suspect, armed, refusing to come down.'; line='They are on the roof and the only way up is the air unit. Ground teams have the building.'; app='none'; armed=$true; backup=$true; stages=@(@{at=50; do='line'; text='Air unit overhead and holding.'; }, @{at=120; do='handsup'; text='Weapon down and on their knees.'; }, @{at=180; do='end'; text='Brought down and in custody.'; }) }
Add @{ id='noose-city-siege'; for='noose'; name='Siege - City Centre Building'; msg='Armed suspects refusing to leave a building in the city centre.'; adv='Two suspects, staff still inside.'; line='Two of them, staff inside, and the street is shut. Nobody goes in until I say.'; app='none'; armed=$true; count=2; backup=$true; stages=@(@{at=60; do='line'; text='Talking to them by phone. They want a car.'; }, @{at=130; do='handsup'; text='The front door is opening.'; }, @{at=200; do='end'; text='Both detained. Staff are out and being checked over.'; }) }
Add @{ id='noose-chemical-find'; for='noose'; name='Chemical Find - Secure It'; msg='Drums found at a disused site, strong smell reported.'; adv='No persons present, wind taking it toward the road.'; line='Nobody is there and the wind is taking it towards the road. That is the problem.'; res='Manual'; backup=$true; amb=$true }

# ============================================================ Federal - IAA

Add @{ id='iaa-consular-incident'; for='iaa'; name='Consular Incident - Protected Property'; msg='Incident at a consular building, staff requesting attendance.'; adv='Crowd outside, no weapons seen.'; line='There are thirty people outside and the staff want a presence, not an arrest.'; app='none'; res='Manual' }
Add @{ id='iaa-arms-embargo'; for='iaa'; name='Arms Embargo - Interdiction'; msg='Consignment believed to breach an arms embargo.'; adv='One lorry, paperwork expected to be false.'; line='The paperwork says machine parts. The weight says something else entirely.'; app='FleeInVehicle'; vehicle='phantom' }
Add @{ id='iaa-detained-national'; for='iaa'; name='Detained Foreign National'; msg='Foreign national detained, identity documents in question.'; adv='One suspect, compliant, documents held.'; line='The passport is either very good or very bad and the difference matters.'; app='none'; res='Manual' }
Add @{ id='iaa-dead-drop'; for='iaa'; name='Dead Drop - Watched Address'; msg='Address watched, believed to be used for exchanges.'; adv='No persons present, bench in the open.'; line='Somebody leaves something and somebody else picks it up. We have not caught either of them yet.'; res='Manual' }
Add @{ id='iaa-diplomatic-escort'; for='iaa'; name='Protected Person - Diplomatic Escort'; msg='Escort required for a protected diplomatic visit.'; adv='Route has been used before, no specific threat.'; line='Two cars and a clear route. If it goes wrong it goes wrong quickly.'; res='Manual'; backup=$true }
Add @{ id='iaa-counter-surveillance'; for='iaa'; name='Counter-Surveillance - Watched Office'; msg='Person repeatedly seen watching an office.'; adv='One suspect, parked up, camera to hand.'; line='Same corner four days running with a camera. That is not tourism.'; app='Flee' }

# ============================================================ Federal - DOA

Add @{ id='doa-stash-house'; for='doa'; name='Narcotics Stash House'; msg='Address believed to be used to hold narcotics.'; adv='Foot traffic through the day, nobody there now.'; line='People in and out all day and never in there longer than ten minutes.'; app='none'; backup=$true }
Add @{ id='doa-deal-interdiction'; for='doa'; name='Deal Interdiction'; msg='Deal about to take place in a car park.'; adv='Two vehicles, both occupants waiting.'; line='Two cars nose to tail with the engines running. That is a handover.'; app='FleeInVehicle'; vehicle='asea'; count=2 }
Add @{ id='doa-grow-warrant'; for='doa'; name='Grow Operation - Warrant'; msg='Warrant for an address with a suspected grow.'; adv='Hum from the property, windows covered.'; line='Humming, covered windows, and a meter nobody has been able to read. The warrant is signed.'; app='none'; backup=$true; stages=@(@{at=30; do='line'; text='Nobody is answering the front door.'; }, @{at=70; do='flee'; text='Somebody has gone out of the back and into the field.'; }, @{at=140; do='end'; text='Property searched and the plant seized.'; }) }
Add @{ id='doa-shore-landing'; for='doa'; name='Smuggling - Shore Landing'; msg='Boat landing at a remote beach reported.'; adv='One vehicle on the beach, lights off.'; line='A boat and a van at that beach at this hour, and neither of them is fishing.'; app='FleeInVehicle'; vehicle='burrito'; armed=$true }
Add @{ id='doa-controlled-buy'; for='doa'; name='Informant Buy - Controlled'; msg='Controlled buy arranged, cover needed.'; adv='One suspect due, informant already on scene.'; line='The buy is set and the informant is stood in the open. Nothing starts until the signal.'; app='none'; backup=$true }
Add @{ id='doa-convoy-stop'; for='doa'; name='Convoy Stop - Suspected Load'; msg='Two-vehicle convoy believed to be carrying narcotics.'; adv='Both vehicles travelling together, rear plate not readable.'; line='Two vehicles running together and the rear plate is unreadable. Stop the front one and the back one stops too.'; app='FleeInVehicle'; vehicle='buffalo'; count=2 }

# ============================================================ North Yankton

Add @{ id='nysp-snowbank'; for='nysp'; name='Vehicle Off the Road - Snowbank'; msg='Vehicle off the road in a snowbank.'; adv='Occupants out of the vehicle, no injuries reported.'; line='They are out and standing next to it, which is the worst place to stand on that road.'; res='Manual' }
Add @{ id='nysp-ice-collision'; for='nysp'; name='Collision - Icy Junction'; msg='Two-vehicle collision on an icy junction.'; adv='Both vehicles blocking a lane.'; line='Black ice on the junction and two cars in it. Nobody is badly hurt and the road is blocked.'; amb=$true }
Add @{ id='nysp-avalanche-closure'; for='nysp'; name='Snow Closure - Traffic Inside It'; msg='Road closed by snow fall, traffic unaware.'; adv='Several vehicles past the closure.'; line='The barrier is up and four vehicles have driven past it. Nobody reads a barrier in the dark.'; res='Manual' }
Add @{ id='nysp-snowmobile-incident'; for='nysp'; name='Snowmobile Incident'; msg='Snowmobile rider injured off the trail.'; adv='Rider on the ground, snowmobile on its side.'; line='They came off it on the bend and they have been down ten minutes.'; amb=$true }
Add @{ id='nysp-hgv-pass'; for='nysp'; name='HGV Check - Mountain Pass'; msg='Lorry stopped for a check, driver refusing to get out.'; adv='Driver in the cab, engine running.'; line='The engine is running and nobody is getting out. There is ice on the pass.'; app='Hostile' }
Add @{ id='nysp-hunter-trespass'; for='nysp'; name='Hunters - Land Without Permission'; msg='Two hunters on land without permission.'; adv='Both armed, rifles slung, no shots fired.'; line='They are the wrong side of the fence line with rifles and they know it.'; app='HandsUp'; armed=$true; weapon='WEAPON_RIFLE' }
Add @{ id='nysp-motel-disturbance'; for='nysp'; name='Motel Disturbance'; msg='Disturbance at a roadside motel.'; adv='Several guests in the car park, one injured.'; line='It has spilled out into the car park and somebody is sat against a car.'; app='Hostile'; count=3; amb=$true }
Add @{ id='nysp-remote-fuel-theft'; for='nysp'; name='Fuel Theft - Remote Station'; msg='Fuel taken from a station with nobody on site.'; adv='Vehicle at the pump with covered plates.'; line='Nobody on site, plates covered with tape, and the pump is still running.'; app='FleeInVehicle'; vehicle='asea' }
Add @{ id='nysp-wrong-way-pass'; for='nysp'; name='Wrong Way - Mountain Pass'; msg='Vehicle driving the wrong way on the pass.'; adv='Several callers, fog reported.'; line='In that fog, on that road, going the wrong way down it. Shut the top and the bottom.'; app='FleeInVehicle'; vehicle='premier' }
Add @{ id='nysp-stranded-motorist'; for='nysp'; name='Stranded Motorist - Welfare Check'; msg='Motorist stranded with no fuel in freezing weather.'; adv='Person in a car with the engine off.'; line='The engine is off and it is well below freezing. That is somebody in trouble, not a parking problem.'; res='Manual' }

# ============================================================ Prison - more

Add @{ id='saspa-workshop-escape'; for='prison'; name='Escape Attempt - Workshop'; msg='Inmate missing from the workshop at the count.'; adv='Tools unaccounted for, perimeter not breached.'; line='One short at the count and a set of tools is missing off the board.'; app='Flee'; backup=$true }
Add @{ id='saspa-phone-find'; for='prison'; name='Contraband Phone - Cell Search'; msg='Phone found during a cell search.'; adv='Inmate identified, cell shared with two others.'; line='It came out of a hollowed book and it has been used tonight.'; res='Manual' }
Add @{ id='saspa-kitchen-incident'; for='prison'; name='Kitchen Incident'; msg='Fight in the kitchens, staff have stepped back.'; adv='Two inmates involved, the rest watching.'; line='Two of them, one with a knife off the block, and the rest of them watching.'; app='Hostile'; count=2; amb=$true }
Add @{ id='saspa-yard-standoff'; for='prison'; name='Yard Standoff - Two Groups'; msg='Two groups refusing to leave the exercise yard.'; adv='Eight or more, no weapons seen yet.'; line='Two groups, neither moving, and the staff will not go in with those numbers.'; app='Hostile'; count=4; backup=$true }
Add @{ id='saspa-visitor-refusal'; for='prison'; name='Visitor Search - Refusal'; msg='Visitor refusing a search at the gate.'; adv='One visitor, staff holding the queue.'; line='They will not be searched and there are forty people in the queue behind them.'; app='none'; res='Manual' }
Add @{ id='saspa-wall-marks'; for='prison'; name='Perimeter Wall - Fresh Marks'; msg='Fresh marks found on the perimeter wall.'; adv='No persons present, lighting failed on one stretch.'; line='Fresh marks and the lights out along that stretch. Somebody is testing it.'; res='Manual'; backup=$true }

# ============================================================ Park Rangers - more

Add @{ id='sapr-abandoned-camp'; for='ranger'; name='Abandoned Campsite'; msg='Campsite abandoned with the belongings still there.'; adv='No persons present, tent up several days.'; line='The tent has been up three days and nobody has been in it since Tuesday.'; res='Manual' }
Add @{ id='sapr-lost-child'; for='ranger'; name='Lost Child - Trail Head'; msg='Child missing from a trail head car park.'; adv='Family on scene, child gone ten minutes.'; line='Ten minutes gone and the family are already heading four different ways. That is how this gets worse.'; backup=$true }
Add @{ id='sapr-illegal-trapping'; for='ranger'; name='Illegal Trapping'; msg='Traps found set along a trail.'; adv='No persons present, several traps armed.'; line='Six of them set along the trail and one has something in it.'; res='Manual'; amb=$true }
Add @{ id='sapr-overdue-boat'; for='ranger'; name='Overdue Boat - Lake'; msg='Boat overdue on the lake, trailer still at the slip.'; adv='One boat, two on board, no radio contact.'; line='The trailer is at the slip and the light went an hour ago.'; res='Manual'; amb=$true; backup=$true }
Add @{ id='sapr-cabin-vandalism'; for='ranger'; name='Vandalism - Ranger Cabin'; msg='Damage to a ranger cabin overnight.'; adv='Door forced, nobody inside.'; line='The door has been put in and there is nothing worth taking in there.'; res='Manual' }
Add @{ id='sapr-fire-ring-breach'; for='ranger'; name='Fire Ring - Restricted Area'; msg='Fire lit in a restricted area overnight.'; adv='Ring still warm, no persons present.'; line='Still warm when I got there, so somebody was sat here an hour ago.'; res='Manual' }

# ============================================================ Los Santos Police - more

Add @{ id='lspd-pharmacy-raid'; for='lspd'; name='Pharmacy Raid'; msg='Controlled drugs taken from a pharmacy overnight.'; adv='Rear fire door forced, alarm not raised.'; line='They came in through the fire door at the back and nobody heard a thing.'; app='Flee'; res='Manual' }
Add @{ id='lspd-cash-van-ambush'; for='lspd'; name='Cash Van Ambush'; msg='Crew ambushed while loading a cash machine.'; adv='Two suspects on foot, cash box taken.'; line='They took the box and went on foot into the estate. The crew are shaken and unhurt.'; app='Flee'; armed=$true; count=2; amb=$true }
Add @{ id='lspd-court-warrant'; for='lspd'; name='Warrant - Failure to Appear'; msg='Person wanted for failing to appear at court.'; adv='Address attended twice, nobody answering.'; line='Two visits and nobody has answered the door. The warrant is still live.'; res='Manual'; app='none'; backup=$true }
Add @{ id='lspd-school-weapon'; for='lspd'; name='Weapon Report - School'; msg='Report of a weapon on school grounds.'; adv='School in lockdown, staff reporting a pupil.'; line='The school has locked down and the staff are describing a pupil. Go in carefully and go in quietly.'; app='HandsUp'; armed=$true; backup=$true }
Add @{ id='lspd-hospital-weapon'; for='lspd'; name='Weapon at the Hospital'; msg='Person in an emergency department with a weapon.'; adv='Staff withdrawn, patients still on the floor.'; line='The staff have pulled back and the patients are still in there. Nobody is going to complain if this is quick.'; app='Hostile'; armed=$true; backup=$true }
Add @{ id='lspd-market-theft-ring'; for='lspd'; name='Theft Ring - Market'; msg='Stallholders reporting repeated thefts from a market.'; adv='Three suspects working the stalls.'; line='Three of them working the stalls in rotation. The traders have had enough and they are watching.'; app='Flee'; count=3 }
Add @{ id='lspd-laundrette-robbery'; for='lspd'; name='Robbery - Laundrette'; msg='Robbery at a laundrette, staff shaken.'; adv='Suspect gone on foot, till taken.'; line='They took the till and ran, and the staff are sat on the floor behind the counter.'; app='Flee' }
Add @{ id='lspd-bookmaker-threats'; for='lspd'; name='Threats - Bookmaker'; msg='Threats made to a bookmaker over debts.'; adv='Two callers, no damage yet.'; line='Two calls this week and both of them mentioned the shop by name.'; res='Manual' }
Add @{ id='lspd-hotel-room-damage'; for='lspd'; name='Hotel Room - Damage and Dispute'; msg='Damage to a hotel room and a dispute over the bill.'; adv='Guest still in the room, staff locked out.'; line='The room is wrecked and the guest will not come out. The staff want the room number and the bill paid.'; app='Hostile' }
Add @{ id='lspd-site-theft'; for='lspd'; name='Theft from a Building Site'; msg='Plant and tools taken from a building site overnight.'; adv='Compound fence cut, machinery missing.'; line='The fence is cut and the digger is gone. That took a low-loader and nobody saw a low-loader.'; res='Manual' }
Add @{ id='lspd-delivery-driver-robbed'; for='lspd'; name='Delivery Driver Robbed'; msg='Delivery driver robbed at the gate of an address.'; adv='Two suspects, one with a knife, gone on foot.'; line='They took the parcels and the phone at the gate. The driver is unhurt and very shaken.'; app='Flee'; armed=$true; count=2 }
Add @{ id='lspd-bus-station-fight'; for='lspd'; name='Fight at the Bus Terminal'; msg='Fight between two groups at a bus terminal.'; adv='Six or more involved, services delayed.'; line='Six of them at the terminal and the buses cannot get in or out.'; app='Hostile'; count=4; amb=$true }
Add @{ id='lspd-marina-break-in'; for='lspd'; name='Break-In at the Marina'; msg='Break-in reported at a marina office.'; adv='Window forced, suspects believed inside.'; line='The office window is in and there is a torch moving about inside.'; app='Flee'; count=2 }
Add @{ id='lspd-subway-robbery'; for='lspd'; name='Robbery on the Subway'; msg='Robbery on a subway platform.'; adv='Suspect gone up the stairs, victim on the platform.'; line='They took the phone on the platform and went up the stairs. The victim is still down there.'; app='Flee' }
Add @{ id='lspd-church-dispute'; for='lspd'; name='Dispute Outside a Church'; msg='Dispute outside a church after a service.'; adv='Two families, no weapons seen.'; line='Two families outside the doors and neither of them is going home first.'; app='Hostile'; count=3 }
Add @{ id='lspd-casino-cheat'; for='lspd'; name='Casino - Suspected Cheating'; msg='Casino staff reporting a suspected cheat at a table.'; adv='One suspect detained by security, no weapons.'; line='Security have them at the table and want it on the record, not on the floor.'; res='Manual' }
Add @{ id='lspd-storage-burglary'; for='lspd'; name='Storage Unit Burglary'; msg='Storage units broken into overnight.'; adv='Several doors forced, nobody on site.'; line='Four units opened in the night and the padlocks are all on the ground.'; res='Manual' }
Add @{ id='lspd-school-dog'; for='lspd'; name='Dog Loose - Schoolyard'; msg='Dog loose in a schoolyard with children at play.'; adv='Staff keeping children inside, dog still loose.'; line='The children are inside and the dog is in the yard. Nobody has been bitten yet.'; app='Hostile' }
Add @{ id='lspd-prescription-fraud'; for='lspd'; name='Prescription Fraud'; msg='Pharmacy reporting fraudulent prescriptions.'; adv='Suspect left details at the counter.'; line='Three prescriptions in a week on the same name and the same handwriting.'; res='Manual' }
Add @{ id='lspd-valet-keys'; for='lspd'; name='Valet - Keys Taken'; msg='Car keys taken from a valet desk.'; adv='Two suspects, one seen going through the desk.'; line='They took the keys off the board, so they can take any car on that lot.'; app='Flee'; count=2 }

# ============================================================ Sheriff - more

Add @{ id='lssd-orchard-theft'; for='sheriff'; name='Theft from an Orchard'; msg='Fruit taken from an orchard overnight.'; adv='Trailer tracks at the gate, nobody present.'; line='They have taken a trailer load in the night and the gate was open for them.'; res='Manual' }
Add @{ id='lssd-quarry-charge'; for='sheriff'; name='Unexploded Charge - Quarry'; msg='Unexploded charge found at a quarry.'; adv='Shot area isolated, contractors stood down.'; line='There is a charge that did not go off and the contractor will not go back in.'; res='Manual'; backup=$true }
Add @{ id='lssd-trailer-park-dispute'; for='sheriff'; name='Trailer Park Dispute'; msg='Long-running dispute between neighbours on a trailer park.'; adv='Shouting in the street, no weapons reported.'; line='Third time this month and it is the same two arguing about the same fence.'; app='Hostile' }
Add @{ id='lssd-irrigation-tamper'; for='sheriff'; name='Irrigation Tampering'; msg='Irrigation line cut, fields without water.'; adv='Valve open, nobody present.'; line='Somebody has opened the valve and cut the line. That is not an accident, that is a message.'; res='Manual' }
Add @{ id='lssd-cattle-truck-spill'; for='sheriff'; name='Cattle Truck - Load Shift'; msg='Cattle truck lost part of its load on a bend.'; adv='Cattle on the road, driver on scene.'; line='They went over the side on the bend and they are all over the road.'; amb=$true }
Add @{ id='lssd-wind-farm-vandal'; for='sheriff'; name='Vandalism - Wind Farm'; msg='Damage to turbines at a wind farm.'; adv='Cables cut, security have nobody on camera.'; line='Two cables cut and the camera does not cover that side of the site.'; res='Manual' }
Add @{ id='lssd-river-camp'; for='sheriff'; name='Illegal Camp - River Bank'; msg='Camp on the river bank on private land.'; adv='Three tents, fire lit, landowner reporting.'; line='Three tents, a fire, and it is on the wrong side of the fence. The owner wants them moved.'; app='none'; res='Manual' }
Add @{ id='lssd-gate-damage'; for='sheriff'; name='Gate Damage - Ranch Entrance'; msg='Ranch entrance gate pulled out of the ground.'; adv='Tyre marks through the gateway, cattle out.'; line='They took the gate off with a vehicle and the cattle are out on the road.'; res='Manual' }
Add @{ id='lssd-horse-trailer-stop'; for='sheriff'; name='Horse Trailer - Suspected Theft'; msg='Horse trailer believed to be carrying stolen animals.'; adv='One vehicle, plates not matching the trailer.'; line='Plate does not match the trailer and there is something alive in the back.'; app='FleeInVehicle'; vehicle='bobcatxl' }
Add @{ id='lssd-farm-shop-robbery'; for='sheriff'; name='Robbery at a Farm Shop'; msg='Robbery at a farm shop, till taken.'; adv='Suspect left in a vehicle, staff unhurt.'; line='They went in at closing with a bar and took the till. Nobody was hurt and the vehicle went south.'; app='FleeInVehicle'; vehicle='asea' }

# ============================================================ Highway Patrol - more

Add @{ id='sahp-motorway-pedestrian'; for='sahp'; name='Pedestrian on the Motorway'; msg='Pedestrian walking in a live lane.'; adv='Several callers, traffic swerving.'; line='Somebody is walking down the hard shoulder and stepping into the lane with the traffic.'; amb=$true }
Add @{ id='sahp-coach-broken-down'; for='sahp'; name='Coach Broken Down'; msg='Coach broken down with passengers aboard.'; adv='Coach on the shoulder, passengers on the verge.'; line='Forty people are standing on the verge next to live traffic. That is the emergency, not the coach.'; res='Manual' }
Add @{ id='sahp-debris-dark'; for='sahp'; name='Debris in the Dark'; msg='Debris in a lane with no lighting.'; adv='Several near misses reported.'; line='Nobody can see it until they are on top of it and it has been reported three times.'; res='Manual' }
Add @{ id='sahp-lorry-no-insurance'; for='sahp'; name='Lorry - No Insurance'; msg='Lorry stopped, documents not in order.'; adv='Driver on scene, load covered.'; line='No insurance, no tax and a load nobody wants looked at.'; res='Manual' }
Add @{ id='sahp-recovery-broken'; for='sahp'; name='Recovery Vehicle Broken Down'; msg='Recovery vehicle broken down while towing.'; adv='Both vehicles on the shoulder, lighting inadequate.'; line='The recovery truck has broken down with a car on the back of it, and it is lit by one cone.'; res='Manual' }
Add @{ id='sahp-bike-filtering'; for='sahp'; name='Motorcycles Filtering at Speed'; msg='Motorcycles filtering through traffic at speed.'; adv='Three bikes, no plates visible.'; line='Three of them going through the traffic like it is standing still, and none of the plates are readable.'; app='FleeInVehicle'; vehicle='bati'; count=3 }
Add @{ id='sahp-tunnel-collision'; for='sahp'; name='Collision in a Tunnel'; msg='Rear-end collision inside a tunnel.'; adv='Two vehicles, lane blocked, smoke reported.'; line='Two cars in it and the lane is blocked inside the tunnel. Smoke was reported and I do not like that.'; amb=$true }
Add @{ id='sahp-bridge-strike'; for='sahp'; name='Bridge Strike - Signage'; msg='Vehicle has struck overhead signage.'; adv='Sign hanging over a live lane.'; line='The sign is hanging down over the inside lane and it is only held by one bolt.'; res='Manual' }

# ============================================================ Medical - more

Add @{ id='ems-asthma-attack'; for='ems'; name='Asthma Attack'; msg='Person unable to breathe properly, inhaler not working.'; adv='Conscious, sitting, speaking in short sentences.'; line='She is sat on the step and cannot get a full sentence out. The inhaler is empty.'; patient='a_f_y_hipster_01'; seconds=12; pline='I can get air in. It is the getting it out that I cannot do.' }
Add @{ id='ems-bee-stings'; for='ems'; name='Multiple Bee Stings'; msg='Person stung repeatedly, swelling spreading.'; adv='Conscious, stung on the arms and neck.'; line='A dozen stings at least and the swelling is moving up the neck.'; patient='a_m_m_farmer_01'; seconds=14; pline='My throat feels thick. That is not right, is it.' }
Add @{ id='ems-electrical-burns'; for='ems'; name='Electrical Burns'; msg='Person burnt by a live cable at a site.'; adv='Power isolated, casualty conscious.'; line='The power is off and the burns are on both hands. That is where the current went in and out.'; patient='a_m_y_construct_01'; seconds=16; pline='My hands feel like they are still holding it.' }
Add @{ id='ems-crushing-injury'; for='ems'; name='Crushing Injury'; msg='Person crushed by machinery, now released.'; adv='Casualty on the ground, colleagues keeping pressure.'; line='They have got him out from under it and his mate is holding a coat over the leg.'; patient='a_m_y_construct_02'; seconds=20; pline='I can feel it. That is good, they told me that is good.' }
Add @{ id='ems-hip-fracture'; for='ems'; name='Suspected Hip Fracture'; msg='Person fallen, unable to stand, hip pain.'; adv='Conscious on the pavement, leg rotated.'; line='She has been down since the morning and the leg is turned out at the foot.'; patient='a_f_y_business_02'; seconds=16; pline='It is the hip. I heard it go before I felt it.' }
Add @{ id='ems-spinal-injury'; for='ems'; name='Suspected Spinal Injury'; msg='Person fallen from a wall, back pain.'; adv='Conscious, not moved, tingling in the legs.'; line='He went off the wall onto the path and he says his legs feel wrong. Nobody has moved him.'; patient='a_m_y_beach_02'; seconds=18; pline='My feet are buzzing. Is that the thing you worry about?' }
Add @{ id='ems-limb-bleeding'; for='ems'; name='Severe Limb Bleeding'; msg='Person bleeding heavily from a leg wound.'; adv='Conscious, pressure applied by a bystander.'; line='The bystander is doing the right thing and has not let go. That is the only reason this is not worse.'; patient='a_m_m_eastsa_01'; seconds=15; pline='It has gone cold. Why has it gone cold?' }
Add @{ id='ems-pregnancy-bleeding'; for='ems'; name='Pregnancy - Bleeding'; msg='Person bleeding in early pregnancy.'; adv='Conscious, distressed, partner present.'; line='She is bleeding and she is frightened, and her partner is more frightened than she is.'; patient='a_f_y_bevhills_01'; seconds=16; pline='Is it the baby? Somebody tell me about the baby.' }

# ============================================================ Fire - more

Add @{ id='fire-church'; for='fire'; name='Fire in a Church'; msg='Fire in the vestry of a church.'; adv='Building being evacuated, no casualties reported.'; line='It is in the vestry and the building is old and dry. Get everybody out of the hall first.'; app='Fire' }
Add @{ id='fire-hotel-evacuation'; for='fire'; name='Hotel Fire - Evacuation'; msg='Fire in a hotel, guests being woken.'; adv='Staff counting guests at the assembly point.'; line='Staff are knocking doors and counting heads at the front. Two rooms are not answering.'; app='Fire'; amb=$true }
Add @{ id='fire-scrapyard'; for='fire'; name='Fire at a Scrapyard'; msg='Fire at a scrapyard, dense smoke.'; adv='No persons on site, wind taking smoke over houses.'; line='The smoke is going straight over the houses and there are cars stacked in there.'; app='Fire' }
Add @{ id='fire-stable-fire'; for='fire'; name='Fire in a Stable Block'; msg='Fire in a stable block with animals inside.'; adv='Horses still in the block, owner on scene.'; line='The horses are still in and the owner is trying to lead them out through the smoke.'; app='Fire'; amb=$true }
Add @{ id='fire-substation'; for='fire'; name='Fire at a Substation'; msg='Fire at an electrical substation.'; adv='Power company on the way, area being cleared.'; line='It is the substation and the power company is twenty minutes out. Keep everybody back from the fence.'; app='Fire' }
Add @{ id='fire-timber-yard'; for='fire'; name='Fire at a Timber Yard'; msg='Fire in a timber yard, spreading through stacks.'; adv='Staff out, stacks well alight.'; line='It has taken two stacks and it is walking along the row. Every stack is the same distance apart.'; app='Fire'; backup=$true }

# ============================================================ Checks, before anything is written
#
# Each of these has caught something real, and each is cheaper than finding it in game. They all fail
# loudly: a library that is missing a script is not a library with a small mistake in it.

$problems = New-Object System.Collections.Generic.List[string]
$seen = @{}

foreach ($entry in $library) {
    $id = [string]$entry['id']
    if (-not $id) { $problems.Add('a recipe has no id'); continue }
    if ($seen.ContainsKey($id)) { $problems.Add("two recipes share the id '$id'") } else { $seen[$id] = $true }

    if (-not $entry['for']) { $problems.Add("$id has no duty - it could only ever be offered as police work") }
    if (-not $entry['name']) { $problems.Add("$id has no name") }
    if (-not $entry['msg']) { $problems.Add("$id has no message") }

    # The point of the dialogue file: every callout has somebody in it who answers when spoken to.
    $talk = [string]$entry['talk']
    if (-not $talk) { $problems.Add("$id has no script - every callout needs one"); continue }

    $spoken = @($talk -split '\s*\|\s*' | Where-Object { $_.Trim().Length -gt 0 })
    if ($spoken.Count -lt 3) { $problems.Add("$id has only $($spoken.Count) script line(s); three is the floor") }
    foreach ($line in $spoken) {
        if ($line.Contains("'")) { $problems.Add("$id has a script line an apostrophe would break: $line") }
    }
}

foreach ($key in $dialogue.Keys) {
    if (-not $seen.ContainsKey([string]$key)) { $problems.Add("the dialogue file has lines for '$key', which is not a callout") }
}

# What the dispatch text says about the person it is sending you to. A pool with both sexes in it and
# prose that says "he" is the pack being wrong about its own scene, which is the whole point of this
# pass - so a recipe whose text is gendered must lock its pool, or reword, or be listed here with the
# reason it cannot. A recipe with a patient is exempt: its pool is the bystander, and the patient's own
# model is fixed and matches what the patient says.
$genderedWords = '\b(he|him|his|she|her|hers|male|males|female|females|man|men|woman|women|boy|boys|girl|girls|guy|guys|lady|ladies|chap)\b'

$aboutSomebodyElse = @(
    'lssd-missing-hiker'    # the woman whose car is at the trailhead is not in the scene; you meet the caller
    'lspd-bag-snatch'       # the woman is the victim who was robbed, not the person you are sent to
    'lspd-phone-snatch'
)

foreach ($entry in $library) {
    $id = [string]$entry['id']

    if ($entry['sex'] -and @('male', 'female') -notcontains $entry['sex']) {
        $problems.Add("$id says sex='$($entry['sex'])', which is neither male nor female")
        continue
    }

    if ($entry['patient'] -or $entry['sex'] -or $entry['models'] -or $entry['actors']) { continue }

    $text = ("" + $entry['name'] + ' ' + $entry['msg'] + ' ' + $entry['adv'] + ' ' + $entry['line'] + ' ' + $entry['brief']).ToLowerInvariant()
    if ($text -notmatch $genderedWords) { continue }
    if ($aboutSomebodyElse -contains $id) { continue }

    $problems.Add("$id describes the person in gendered words but draws from both - reword it, lock it with sex='male', or add it to aboutSomebodyElse with a reason")
}

# The people: a pool has to be a pool, and a name that cannot be a ped model name is a spawn that
# silently falls back - which is survivable, and still worth not shipping.
foreach ($entry in $library) {
    $pool = [string]$entry['models']
    if (-not $pool) { continue }

    if (-not $entry['pmodel'] -and -not $entry['models']) { $problems.Add("$($entry['id']) has a pool but no model to fall back to if the pool is wrong for an install") }
    $names = @($pool -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    if ($names.Count -lt 2) { $problems.Add("$($entry['id']) has a pool of $($names.Count) - pools are for more than one") }
    if (($names | Sort-Object -Unique).Count -ne $names.Count) { $problems.Add("$($entry['id']) names the same person twice in its pool") }
    foreach ($name in $names) {
        if ($name -notmatch '^[a-z0-9_]+$') { $problems.Add("$($entry['id']) has '$name' in its pool, which is not a model name") }
    }
}

# The vehicles, by the same rules.
foreach ($entry in $library) {
    $vehicles = Vehicles-For $entry
    if (-not $vehicles) { continue }

    if (-not $entry['vehicle']) { $problems.Add("$($entry['id']) has a vehicle pool but no vehicle to fall back to if the pool is wrong for an install") }
    $names = @($vehicles -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    if ($names.Count -lt 2) { $problems.Add("$($entry['id']) has a vehicle pool of $($names.Count) - pools are for more than one") }
    if (($names | Sort-Object -Unique).Count -ne $names.Count) { $problems.Add("$($entry['id']) names the same vehicle twice in its pool") }
    foreach ($name in $names) {
        if ($name -notmatch '^[a-z0-9_]+$') { $problems.Add("$($entry['id']) has '$name' in its vehicle pool, which is not a model name") }
    }
}

# A patient's own model is fixed rather than drawn, so the dispatch text has to agree with the person
# whose face it will be. 'a_f_' is a woman, 'a_m_' is a man, and the pack's own text has to be one or the
# other - this is what caught a call saying "she" about a man on the floor.
foreach ($entry in $library) {
    if (-not $entry['patient']) { continue }

    $id = [string]$entry['id']
    $model = [string]$entry['patient']
    $text = ("" + $entry['name'] + ' ' + $entry['msg'] + ' ' + $entry['adv'] + ' ' + $entry['line'] + ' ' + $entry['brief']).ToLowerInvariant()

    $saysWoman = $text -match '\b(she|her|hers|woman)\b'
    $saysMan = $text -match '\b(he|him|his|man)\b'

    if ($saysWoman -and $model -notlike 'a_f_*') { $problems.Add("$id calls the patient 'she' but spawns $model") }
    if ($saysMan -and $model -notlike 'a_m_*') { $problems.Add("$id calls the patient 'he' but spawns $model") }
}

if ($problems.Count -gt 0) {
    foreach ($problem in $problems) { Write-Host ("  " + $problem) -ForegroundColor Red }
    throw ("the library will not be written: " + $problems.Count + " problem(s) above")
}

Write-Host ("checks passed: " + $library.Count + " recipes, every one with a script") -ForegroundColor Green

# ============================================================ Writing them out
function Xml($text) {
    if ($null -eq $text) { return '' }
    return ([string]$text).Replace('&', '&amp;').Replace('<', '&lt;').Replace('>', '&gt;')
}

$written = 0
foreach ($r in $library) {
    $kind = if ($r['for']) { $r['for'] } else { 'police' }
    $probability = if ($r['prob']) { $r['prob'] } else { 'Medium' }
    $resolution = if ($r['res']) { $r['res'] } else { 'ArrestOrDeath' }

    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add('<?xml version="1.0" encoding="utf-8"?>')
    $lines.Add('<!-- Written by tools\make-callout-library.ps1 - edit that, not this file. -->')
    $lines.Add('<Callout>')
    $lines.Add("    <Id>$(Xml $r['id'])</Id>")
    $lines.Add("    <Name>$(Xml $r['name'])</Name>")
    $lines.Add("    <For>$kind</For>")
    $lines.Add("    <Probability>$probability</Probability>")
    $lines.Add("    <Resolution>$resolution</Resolution>")
    $lines.Add("    <Message>$(Xml $r['msg'])</Message>")
    if ($r['adv']) { $lines.Add("    <Advisory>$(Xml $r['adv'])</Advisory>") }
    $lines.Add('    <Distance Min="150" Max="320" Radius="40" />')

    $actors = $r['actors']
    if (-not $actors) {
        $count = if ($r['count']) { $r['count'] } else { 1 }
        # A recipe with a patient has nobody to arrest, so the person standing there is the caller or
        # the family rather than a suspect. That is also what decides who speaks when the player talks:
        # the first suspect if there is one, otherwise the patient - see Custom\RecipeCallout.cs.
        # The model a pool falls back to matches the pool's sex: a callout locked to women falls back to
        # a woman, so the fallback cannot contradict the callout's own description of them.
        $actors = @(@{
            model   = if ($r['pmodel']) { $r['pmodel'] }
                      elseif ($r['sex'] -eq 'female') { 'a_f_y_business_01' }
                      else { 'a_m_y_business_01' }
            models  = if ($r['pmodel']) { $null } else { People-For $r }
            role    = if ($r['patient']) { 'Bystander' } else { 'Suspect' }
            armed   = $r['armed']
            weapon  = if ($r['weapon']) { $r['weapon'] } else { 'WEAPON_PISTOL' }
            hostile = ($r['app'] -eq 'Hostile')
            cower   = $false
            vehicle = $r['vehicle']
            vehicles = Vehicles-For $r
            count   = $count
        })
    }

    $lines.Add('    <Actors>')
    foreach ($a in $actors) {
        $attributes = @("Count=`"$(if ($a['count']) { $a['count'] } else { 1 })`"")
        # A pool, and the single model it falls back to, written as two attributes. Both belong there:
        # the pack spawns through one path either way, and a pool that turns out to be wrong for an
        # install still has to leave somebody standing in the scene. Without the fallback a bad pool is
        # an empty street, and an empty street closes the moment it starts.
        if ($a['models']) {
            $attributes = @("Models=`"$($a['models'])`"") + $attributes
            if ($a['model']) { $attributes = @("Model=`"$($a['model'])`"") + $attributes }
        }
        else { $attributes = @("Model=`"$($a['model'])`"") + $attributes }
        if ($a['role']) { $attributes += "Role=`"$($a['role'])`"" }
        if ($a['armed']) { $attributes += 'Armed="true"'; $attributes += "Weapon=`"$($a['weapon'])`"" }
        if ($a['hostile']) { $attributes += 'Hostile="true"' }
        if ($a['cower']) { $attributes += 'Cower="true"' }
        # The same for the vehicle: the pool, and the one the recipe named as the last resort. A pooled
        # actor with no fallback arrives on foot, which is a car chase that does not happen.
        if ($a['vehicles']) {
            $attributes += "Vehicles=`"$($a['vehicles'])`""
            if ($a['vehicle']) { $attributes += "Vehicle=`"$($a['vehicle'])`"" }
        }
        elseif ($a['vehicle']) { $attributes += "Vehicle=`"$($a['vehicle'])`"" }
        $lines.Add('        <Ped ' + ($attributes -join ' ') + ' />')
    }
    $lines.Add('    </Actors>')

    $approach = $r['app']
    if ($approach -and $approach -ne 'none') {
        $flags = switch ($approach) {
            'Flee'          { 'Flee="true"' }
            'FleeInVehicle' { 'FleeInVehicle="true"' }
            'Hostile'       { 'Hostile="true"' }
            'HandsUp'       { 'HandsUp="true"' }
            'Cower'         { 'Cower="true"' }
            'Fire'          { 'Fire="true"' }
            default         { '' }
        }
        $at = if ($r['appat']) { $r['appat'] } else { '25' }
        $lines.Add("    <OnApproach At=`"$at`" $flags />")
    }

    $support = @()
    if ($r['amb']) { $support += 'Ambulance="true"' }
    if ($r['backup']) { $support += 'Backup="true"' }
    if ($support.Count -gt 0) { $lines.Add('    <Support ' + ($support -join ' ') + ' />') }

    # What they say when they are spoken to. Piped on one line in the table: 'First line | second line'.
    if ($r['talk']) {
        $lines.Add('    <Script>')
        foreach ($spoken in ($r['talk'] -split '\s*\|\s*')) {
            if ($spoken.Trim().Length -gt 0) { $lines.Add("        <Line>$(Xml $spoken.Trim())</Line>") }
        }
        $lines.Add('    </Script>')
    }

    # What happens after the player has been there a while.
    if ($r['stages']) {
        $lines.Add('    <Stages>')
        foreach ($stage in $r['stages']) {
            $lines.Add("        <Stage At=`"$($stage['at'])`" Do=`"$($stage['do'])`">$(Xml $stage['text'])</Stage>")
        }
        $lines.Add('    </Stages>')
    }
    if ($r['patient']) {
        $lines.Add("    <Patient Model=`"$($r['patient'])`" TreatmentSeconds=`"$($r['seconds'])`">$(Xml $r['pline'])</Patient>")
    }

    $lines.Add('    <Lines>')
    if ($r['brief']) { $lines.Add("        <Briefing>$(Xml $r['brief'])</Briefing>") }
    if ($r['line']) { $lines.Add("        <Approach>$(Xml $r['line'])</Approach>") }
    if ($r['settle']) { $lines.Add("        <Resolved>$(Xml $r['settle'])</Resolved>") }
    $lines.Add('        <SignOff>Show me 10-8 when you are clear.</SignOff>')
    $lines.Add('    </Lines>')
    $lines.Add('</Callout>')

    [System.IO.File]::WriteAllLines((Join-Path $OutputDirectory ($r['id'] + '.xml')), $lines)
    $written++
}

Write-Host ("wrote " + $written + " recipes to " + $OutputDirectory) -ForegroundColor Green
foreach ($group in ($library | Group-Object { $_['for'] } | Sort-Object Name)) {
    Write-Host ("   " + $group.Name.PadRight(18) + $group.Count)
}
