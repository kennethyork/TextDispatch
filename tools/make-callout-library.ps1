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
function Add($entry) { $script:library += $entry }

# ============================================================ Los Santos Police
Add @{ id='lspd-armed-robbery-off-licence'; for='lspd'; name='Armed Robbery - Off Licence'; msg='Armed robbery in progress at an off licence.'; adv='One male with a handgun, staff inside, no shots fired yet.'; line='One male, handgun, in the off licence now. Get there before he leaves.'; brief='Armed robbery in progress. He is still inside and the staff are still in there with him.'; app='Hostile'; armed=$true }
Add @{ id='lspd-armed-robbery-pharmacy'; for='lspd'; name='Armed Robbery - Pharmacy'; msg='Armed robbery reported at a pharmacy.'; adv='Suspect left on foot towards the estate, dark hooded top.'; line='Male, dark hooded top, gone on foot towards the estate.'; app='Flee'; armed=$true; res='AnyArrest' }
Add @{ id='lspd-bank-robbery'; for='lspd'; name='Bank Robbery in Progress'; msg='Silent alarm at a bank, staff not answering the phone.'; adv='Two males inside, cash seen being bagged.'; line='Silent alarm, no answer from the branch. Two males inside. Do not go in alone.'; brief='A bank, a silent alarm, and nobody answering the phone. Assume two armed and treat it that way.'; app='Hostile'; armed=$true; count=2; backup=$true }
Add @{ id='lspd-cash-in-transit'; for='lspd'; name='Cash in Transit Robbery'; msg='Armoured van crew robbed during a delivery.'; adv='Two suspects on a black motorbike, cash box taken.'; line='Cash box taken, two on a black bike. Follow it, do not force it.'; app='FleeInVehicle'; armed=$true; vehicle='akuma'; count=2 }
Add @{ id='lspd-carjacking'; for='lspd'; name='Carjacking in Progress'; msg='Driver pulled from their car at a junction.'; adv='Suspect drove off in the victim vehicle, northbound.'; line='They took the car and went north. Victim is shaken but on his feet.'; app='FleeInVehicle'; vehicle='sultan'; armed=$true }
Add @{ id='lspd-shots-fired-residential'; for='lspd'; name='Shots Fired - Residential'; msg='Several shots heard on a residential street.'; adv='No victim found by the caller, nobody answering doors.'; line='Four or five shots, no victim yet, and nobody is answering their door.'; app='HandsUp'; armed=$true; count=2 }
Add @{ id='lspd-man-with-a-gun'; for='lspd'; name='Man with a Gun'; msg='Man seen with a firearm at a bus stop.'; adv='No shots fired, handgun in his waistband.'; line='Caller says it is in his waistband. No shots yet.'; app='Hostile'; armed=$true }
Add @{ id='lspd-drive-by'; for='lspd'; name='Drive-By Shooting'; msg='Drive-by shooting reported from a moving vehicle.'; adv='Vehicle left, one person injured on the pavement.'; line='Car has gone, one injured on the pavement. Ambulance is rolling with you.'; amb=$true }
Add @{ id='lspd-armed-standoff'; for='lspd'; name='Armed Suspect Refusing to Comply'; msg='Armed suspect refusing to comply, officers holding back.'; adv='Suspect in a yard, weapon visible, no shots exchanged.'; line='He will not put it down and he will not come out. Back-up is two minutes out.'; app='Hostile'; armed=$true; backup=$true }
Add @{ id='lspd-drug-deal'; for='lspd'; name='Drug Deal in Progress'; msg='Drug deal in progress in an alley, caller watching.'; adv='Two males, exchange seen twice.'; line='Two males in the alley, second exchange in ten minutes.'; app='Flee' }
Add @{ id='lspd-meth-lab'; for='lspd'; name='Possible Methamphetamine Lab'; msg='Strong chemical smell from a rented property.'; adv='No persons seen, landlord reporting through the letterbox.'; line='Landlord says the smell has been there a week and nobody has been seen going in.'; backup=$true }
Add @{ id='lspd-dealing-crew'; for='lspd'; name='Street Dealing - Group'; msg='Group dealing in the open on a corner.'; adv='Three or four males, one keeping watch.'; line='Four of them and one watching the corner. They will scatter when they see you.'; app='Flee'; count=4; armed=$true; weapon='WEAPON_KNIFE' }
Add @{ id='lspd-burglary'; for='lspd'; name='Burglary in Progress'; msg='Neighbour reporting a break-in next door.'; adv='One male, rear window forced, still inside.'; line='Rear window is in and he is still inside. The neighbours are watching from their kitchen.'; app='Flee' }
Add @{ id='lspd-car-break-in'; for='lspd'; name='Vehicle Break-In'; msg='Person seen breaking into a parked car.'; adv='Male on foot, window smashed, owner not on scene.'; line='He smashed the window and he is still at it. Owner is not with us yet.'; app='Flee' }
Add @{ id='lspd-graffiti'; for='lspd'; name='Vandalism - Graffiti'; msg='Two people spraying graffiti on a wall.'; adv='Both on foot, cans in hand.'; line='Two of them, cans in hand, on the wall by the underpass.'; app='Flee'; count=2 }
Add @{ id='lspd-theft-in-progress'; for='lspd'; name='Theft in Progress'; msg='Person filling a bag with stock and walking out.'; adv='Staff following at a distance, no violence.'; line='They are following him at a distance. No violence so far - keep it that way.'; app='Flee' }
Add @{ id='lspd-assault-in-progress'; for='lspd'; name='Assault in Progress'; msg='Assault in progress in the street, caller watching from a window.'; adv='One male attacking another, both on foot.'; line='One male on another in the street, and the caller will not come out.'; app='Hostile'; amb=$true }
Add @{ id='lspd-stabbing'; for='lspd'; name='Stabbing Reported'; msg='Stabbing reported, victim conscious and bleeding.'; adv='Suspect ran towards the park, no weapon seen.'; line='Victim is conscious and losing blood. Suspect ran for the park, and an ambulance is coming.'; app='Flee'; amb=$true }
Add @{ id='lspd-bar-fight'; for='lspd'; name='Assault - Bar Fight'; msg='Fight outside a bar, one person on the ground.'; adv='Several involved, staff trying to separate them.'; line='Fight outside the bar, more than two involved, and one of them is down.'; app='Hostile'; count=3; amb=$true }
Add @{ id='lspd-drunk-disorderly'; for='lspd'; name='Drunk and Disorderly'; msg='Drunk male shouting at passers-by outside a bar.'; adv='No violence yet, staff want him moved on.'; line='No violence, they just want him moved on. He has other ideas.'; app='Hostile' }
Add @{ id='lspd-fight-in-street'; for='lspd'; name='Fight in the Street'; msg='Two groups fighting in the street.'; adv='Six or more involved, bottles seen.'; line='Six of them at least, and bottles. Wait for your back-up if you can.'; app='Hostile'; count=4; backup=$true; amb=$true }
Add @{ id='lspd-noise-complaint'; for='lspd'; name='Noise Complaint'; msg='Repeated noise complaint from a residential address.'; adv='Loud music, several neighbours have called.'; line='Fifth call this week from the same neighbours.'; res='Manual' }
Add @{ id='lspd-suspicious-package'; for='lspd'; name='Suspicious Package'; msg='Unattended bag reported outside a building.'; adv='No persons near it, staff clearing the building themselves.'; line='Nobody near it and the staff are clearing the building themselves. Do not go poking at it.'; res='Manual'; backup=$true }
Add @{ id='lspd-counterfeit-currency'; for='lspd'; name='Counterfeit Currency'; msg='Shop reporting a customer passing counterfeit notes.'; adv='Customer still in the shop, staff holding the note.'; line='Staff have the note and the customer is still in there. They want a report, not a scene.' }
Add @{ id='lspd-fraud-report'; for='lspd'; name='Fraud Report'; msg='Business reporting a fraudulent transaction.'; adv='Suspect left some time ago, details with staff.'; line='He is long gone but the staff have everything written down for you.'; res='Manual' }
Add @{ id='lspd-aggressive-dog'; for='lspd'; name='Aggressive Dog'; msg='Dog attacking people in a park.'; adv='Two people bitten, dog still loose.'; line='Two people bitten and the dog is still loose. Ambulance is coming for them.'; app='Hostile'; amb=$true }
Add @{ id='lspd-missing-person'; for='lspd'; name='Missing Person Report'; msg='Missing person report, last seen this morning.'; adv='Adult male, no medical condition reported, on foot.'; line='Last seen this morning on foot. Family are out looking as well.'; res='Manual' }
Add @{ id='lspd-person-in-crisis'; for='lspd'; name='Person in Crisis'; msg='Caller worried about a person who has threatened to harm themselves.'; adv='Person on foot, no weapon, no crime reported.'; line='Nobody has committed an offence here. He is on his own and somebody has to talk to him.'; res='Manual' }
Add @{ id='all-officer-needs-assistance'; for='lspd,sheriff,sahp'; name='Officer Needs Assistance'; msg='Officer requesting immediate assistance, shots fired.'; adv='Officer on foot, suspect armed, no further detail.'; line='Shots fired and an officer is calling for help. Go, and go carefully.'; app='Hostile'; armed=$true; count=2; backup=$true; amb=$true }
Add @{ id='all-assault-on-officer'; for='lspd,sheriff,sahp'; name='Assault on an Officer'; msg='Officer assaulted during a stop, suspect restrained by a member of the public.'; adv='Suspect held on the ground, officer with a facial injury.'; line='A member of the public has him on the ground. The officer took one to the face.'; amb=$true }

# ============================================================ Sheriff, county
Add @{ id='lssd-ranch-trespass'; for='sheriff'; name='Trespasser on Ranch Land'; msg='Rancher reporting somebody on his land with a rifle.'; adv='Male on foot, rifle slung, no shots fired.'; line='He is on the rancher land with a rifle on his shoulder. Nobody has been threatened yet.'; app='HandsUp'; armed=$true; weapon='WEAPON_RIFLE' }
Add @{ id='lssd-livestock-theft'; for='sheriff'; name='Livestock Theft'; msg='Cattle reported taken from a field overnight.'; adv='Trailer tracks at the gate, no suspects present.'; line='The gate was cut and the tracks are fresh. Whoever did it is long gone.'; res='Manual' }
Add @{ id='lssd-poaching'; for='sheriff'; name='Poaching Reported'; msg='Shots heard on protected land after dark.'; adv='Vehicle parked at the treeline, no persons seen.'; line='Shots after dark and a truck at the treeline. Take it slowly, they will have rifles.'; app='Flee'; armed=$true; weapon='WEAPON_RIFLE' }
Add @{ id='lssd-rural-burglary'; for='sheriff'; name='Rural Burglary'; msg='Farmhouse broken into while the owners were out.'; adv='Rear door forced, suspects gone, owners returning.'; line='They came in through the back and they are gone. The owners are on their way home.'; res='Manual' }
Add @{ id='lssd-dirt-bikes'; for='sheriff'; name='Off-Road Bikes Reported'; msg='Group of dirt bikes riding through a residential area.'; adv='Four or five bikes, no plates, riders masked.'; line='Four or five of them, no plates, and they know every track around here.'; app='FleeInVehicle'; vehicle='sanchez'; count=3 }
Add @{ id='lssd-illegal-dumping'; for='sheriff'; name='Illegal Dumping'; msg='Van seen tipping waste on a back road.'; adv='Van still on scene, one male, no weapons seen.'; line='The van is still there and he is still unloading it. He has not seen anybody.'; app='FleeInVehicle'; vehicle='burrito' }
Add @{ id='lssd-desert-lab'; for='sheriff'; name='Possible Lab in the Desert'; msg='Chemical smell and generators reported at a remote property.'; adv='No persons seen, lights on at night.'; line='Generators running all night and a smell you would not forget. Nobody has seen a soul.'; backup=$true }
Add @{ id='lssd-sandy-shores-fight'; for='sheriff'; name='Fight - Sandy Shores'; msg='Fight outside a bar in Sandy Shores.'; adv='Four involved, two on the ground.'; line='Four of them and two already on the floor. It is a small town and they all know each other.'; app='Hostile'; count=4; amb=$true }
Add @{ id='lssd-domestic-county'; for='sheriff'; name='Domestic Disturbance - County'; msg='Domestic disturbance reported at a rural address.'; adv='Caller is a neighbour, shouting heard, no weapons reported.'; line='The neighbour called it in. Shouting, no weapons mentioned, and no answer from the house.'; app='Hostile'; amb=$true }
Add @{ id='lssd-moonshine-still'; for='sheriff'; name='Suspected Still'; msg='Suspected illegal distillery in the hills.'; adv='Copper tubing and drums seen from the road.'; line='Tubing, drums and a fire going. Somebody has been at this a while.'; backup=$true }
Add @{ id='lssd-wanted-person'; for='sheriff'; name='Wanted Person Seen'; msg='Wanted person seen on a trail by a hiker.'; adv='Male on foot, description matches a recent warrant.'; line='The hiker is sure it was him. He is on foot and he is in no hurry.'; app='Flee'; backup=$true }
Add @{ id='lssd-stolen-tractor'; for='sheriff'; name='Stolen Agricultural Vehicle'; msg='Tractor reported taken from a farm overnight.'; adv='Tracks lead towards the highway, no suspect seen.'; line='It cannot have gone far at that speed. Tracks head for the highway.' }
Add @{ id='lssd-missing-hiker'; for='sheriff,ranger'; name='Missing Hiker'; msg='Hiker overdue by several hours.'; adv='Car still at the trailhead, weather turning.'; line='Her car is still at the trailhead and the weather is coming in. That is all we have.'; res='Manual'; amb=$true }
Add @{ id='lssd-prisoner-escape'; for='sheriff,prison'; name='Prisoner Escape'; msg='Prisoner has escaped from custody during transport.'; adv='Suspect on foot in cuffs, last seen crossing the road.'; line='He is still in cuffs and he is on foot. He cannot have gone far.'; app='Flee'; backup=$true }

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
Add @{ id='sapr-lost-walker'; for='ranger'; name='Lost Walker'; msg='Walker on the phone and lost, battery nearly flat.'; adv='Knows the trailhead, does not know where she is.'; line='She is on the phone and the battery is going. We have a trailhead and nothing else.'; res='Manual' }
Add @{ id='sapr-injured-wildlife'; for='ranger'; name='Injured Wildlife'; msg='Deer struck by a vehicle, still alive at the roadside.'; adv='Animal in the road, traffic slowing.'; line='It is still on its feet and it is in the road. Drivers are slowing down to look.'; res='Manual' }
Add @{ id='sapr-off-roading'; for='ranger'; name='Illegal Off-Roading'; msg='Vehicles driving off the marked trails.'; adv='Two vehicles, tracks through protected ground.'; line='Two of them cutting through protected ground. The tracks are fresh.'; app='FleeInVehicle'; vehicle='rebel' }
Add @{ id='sapr-dumping-park'; for='ranger'; name='Dumping in the Park'; msg='Waste dumped at a trailhead overnight.'; adv='No vehicle present, no witnesses.'; line='Somebody tipped a trailer-load at the trailhead and left. No witnesses, no vehicle.'; res='Manual' }

# ============================================================ Prison
Add @{ id='saspa-escape-grounds'; for='prison'; name='Escape - Prison Grounds'; msg='Inmate missing at roll call, believed still on the grounds.'; adv='Description circulated, no vehicle reported.'; line='One missing at roll call and no vehicle reported, so he is on foot and he is close.'; app='Flee'; backup=$true }
Add @{ id='saspa-contraband-drop'; for='prison'; name='Contraband Drop'; msg='Suspected contraband drop at the perimeter.'; adv='Vehicle seen slowing at the fence, package recovered.'; line='Something came over the fence and we have it. The vehicle that threw it did not stop.'; app='FleeInVehicle'; vehicle='premier' }
Add @{ id='saspa-transport-incident'; for='prison'; name='Transport Incident'; msg='Prisoner transport stopped, inmate refusing to comply.'; adv='Inmate secured in the van, two officers on scene.'; line='The van is stopped and he will not walk. Two officers are with him and they want a hand.'; backup=$true }
Add @{ id='saspa-assault-on-staff'; for='prison'; name='Assault on Staff'; msg='Officer assaulted during a cell move.'; adv='Inmate restrained, officer with a head injury.'; line='He is restrained now but the officer took one to the head. Ambulance is coming.'; amb=$true }

# ============================================================ Medical - EMS duty
Add @{ id='ems-fall-elderly'; for='ems'; name='Fall - Elderly Resident'; msg='Elderly person fallen at home, cannot get up.'; adv='Conscious, hip pain, neighbour has the key.'; line='She has been on the floor since this morning and the neighbour has the key.'; patient='a_m_m_tramp_01'; seconds=12; pline='I cannot put any weight on it. Where is the ambulance?' }
Add @{ id='ems-diabetic'; for='ems'; name='Diabetic Emergency'; msg='Diabetic person unwell, conscious but confused.'; adv='Family present, no insulin taken today.'; line='The family are with her. She is confused and has not eaten today.'; patient='a_f_y_business_01'; seconds=14; pline='I just need something sweet. I know what it is.' }
Add @{ id='ems-seizure'; for='ems'; name='Seizure'; msg='Person having a seizure in the street.'; adv='Caller keeping people back, seizure ongoing.'; line='It is still going on. The caller is keeping everybody back like they were told to.'; patient='a_m_y_vinewood_01'; seconds=16; pline='What happened? Why is everybody looking at me?' }
Add @{ id='ems-respiratory'; for='ems'; name='Respiratory Distress'; msg='Person struggling to breathe, inhaler empty.'; adv='Conscious, sitting up, speaking in short sentences.'; line='She is sitting up and cannot finish a sentence. Inhaler is empty.'; patient='a_f_y_hipster_01'; seconds=12; pline='I can breathe again. I really could not get it in.' }
Add @{ id='ems-chest-pain'; for='ems'; name='Chest Pain'; msg='Person with chest pain, history of heart problems.'; adv='Conscious, pale, sitting on a bench.'; line='Pale, sweating, and he has a history. Do not let him walk anywhere.'; patient='a_m_m_business_01'; seconds=18; pline='It is easing. It does not feel like the last one, but it does not feel right.' }
Add @{ id='ems-assault-victim'; for='ems'; name='Assault Victim'; msg='Person assaulted, bleeding from a head wound.'; adv='Suspect gone, victim on the pavement, conscious.'; line='Suspect has gone. The victim is on the pavement and the head wound is a bad one.'; patient='a_m_y_business_01'; seconds=15; pline='I did not even see him. Is it bad?' }
Add @{ id='ems-pedestrian-struck'; for='ems'; name='Pedestrian Struck'; msg='Pedestrian struck by a vehicle, driver still on scene.'; adv='Casualty in the road, road blocked.'; line='Casualty in the road and the driver is standing there in shock. Two patients for you.'; patient='a_m_m_hasjew_01'; seconds=20; pline='My leg. I cannot feel my leg properly.' }
Add @{ id='ems-cyclist-down'; for='ems'; name='Cyclist Down'; msg='Cyclist off a bicycle, suspected fracture.'; adv='Conscious, helmet on, bicycle in the road.'; line='He came off at the junction. Helmet is still on him and he is talking.'; patient='a_m_y_skater_01'; seconds=12; pline='The bike is fine. I am not sure the arm is.' }
Add @{ id='ems-heat-exhaustion'; for='ems'; name='Heat Exhaustion'; msg='Person collapsed in the heat, no shade nearby.'; adv='Conscious but confused, no water.'; line='He has been out in it all day and there is no shade where he is.'; patient='a_m_y_beach_01'; seconds=12; pline='I just need to sit down for a minute. I am fine.' }
Add @{ id='ems-exposure'; for='ems,ranger'; name='Exposure - Cold'; msg='Walker found cold and disoriented on the ridge.'; adv='Shivering stopped, caller has him out of the wind.'; line='He is out of the wind with the caller. The shivering has stopped, which is not good news.'; patient='a_m_m_farmer_01'; seconds=14; pline='I lost the path in the fog. I thought I was going the right way.' }
Add @{ id='ems-overdose-park'; for='ems'; name='Overdose in a Park'; msg='Person unresponsive on a bench, paraphernalia nearby.'; adv='Breathing but not waking, no witnesses.'; line='Breathing but not waking, and nobody with him. You are the first one there.'; patient='a_m_m_trampbeac_01'; seconds=12; pline='Where - what day is it? Did somebody call you?' }
Add @{ id='ems-fall-from-height'; for='ems'; name='Fall from Height'; msg='Tradesman fallen from a ladder, conscious.'; adv='Casualty on the ground, colleague applying pressure.'; line='He went off the third rung carrying something. His mate is holding pressure.'; patient='a_m_y_construct_01'; seconds=16; pline='I landed on my side. I can move my fingers. That is something.' }
Add @{ id='ems-domestic-injury'; for='ems'; name='Injury - Domestic'; msg='Person with an injury following a domestic incident.'; adv='Patient safe with a neighbour, injury to the arm.'; line='She is next door with the neighbour. The injury needs looking at and so does she.'; patient='a_f_y_vinewood_01'; seconds=14; pline='It is not as bad as it looks. He has gone.' }
Add @{ id='ems-broken-ankle'; for='ems,ranger'; name='Walker with a Broken Ankle'; msg='Walker unable to continue, suspected broken ankle.'; adv='Ridge path, no road access, weather closing in.'; line='She is on the ridge path and there is no road up there. Take the kit you can carry.'; patient='a_f_y_hipster_01'; seconds=18; pline='I heard it go. I am not walking down from here.' }

# ============================================================ Fire duty
Add @{ id='fire-refuse-fire'; for='fire'; name='Refuse Fire'; msg='Bins on fire behind a row of shops.'; adv='Two bins alight, no structures involved.'; line='Two bins behind the shops. Get there before it reaches the fence.'; app='Fire' }
Add @{ id='fire-kitchen-fire'; for='fire'; name='Kitchen Fire'; msg='Kitchen fire in a flat, occupants out.'; adv='Occupants evacuated by a neighbour, smoke from the window.'; line='Everybody is out and the neighbour did the smoke alarm part. Smoke from the back window.'; app='Fire' }
Add @{ id='fire-grass-fire'; for='fire'; name='Grass Fire'; msg='Grass fire on the hillside, wind picking up.'; adv='No property at risk yet, walking trail nearby.'; line='Hillside, wind getting up, and there is a walking trail on the other side of it.'; app='Fire' }
Add @{ id='fire-vehicle-car-park'; for='fire'; name='Vehicle Fire'; msg='Vehicle alight in a car park, one person overcome by smoke.'; adv='Nobody in the vehicle, one casualty being kept back.'; line='Nobody is in the car. One person took the smoke and is being kept clear.'; app='Fire'; patient='a_m_y_business_01'; seconds=10; pline='I was right next to it. My chest feels tight.' }
Add @{ id='fire-commercial-bins'; for='fire'; name='Commercial Rubbish Fire'; msg='Fire in the bins behind a business, spreading to a fence.'; adv='Staff using extinguishers, fence beginning to catch.'; line='Staff have extinguishers out but the fence is starting to go. Get there.'; app='Fire' }
Add @{ id='fire-vehicle-roadside'; for='fire'; name='Vehicle Fire - Roadside'; msg='Car alight on the hard shoulder, occupants out.'; adv='Occupants well clear, traffic passing slowly.'; line='Everybody is out of it and standing well back. Traffic is slowing to look, which is its own problem.'; app='Fire' }

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
        $actors = @(@{
            model   = if ($r['pmodel']) { $r['pmodel'] } else { 'a_m_y_business_01' }
            armed   = $r['armed']
            weapon  = if ($r['weapon']) { $r['weapon'] } else { 'WEAPON_PISTOL' }
            hostile = ($r['app'] -eq 'Hostile')
            cower   = $false
            vehicle = $r['vehicle']
            count   = $count
        })
    }

    $lines.Add('    <Actors>')
    foreach ($a in $actors) {
        $attributes = @("Model=`"$($a['model'])`"", "Count=`"$($a['count'])`"")
        if ($a['armed']) { $attributes += 'Armed="true"'; $attributes += "Weapon=`"$($a['weapon'])`"" }
        if ($a['hostile']) { $attributes += 'Hostile="true"' }
        if ($a['cower']) { $attributes += 'Cower="true"' }
        if ($a['vehicle']) { $attributes += "Vehicle=`"$($a['vehicle'])`"" }
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
