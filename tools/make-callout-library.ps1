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

# ============================================================ The other packs' callouts, so they can come out
#
# Every callout 686 Callouts 2.1.2 offers (its thirty English translation files) and every one United
# Callouts enables, as close as a recipe can get to each. Where the original is a scripted set-piece -
# a car bomb on a timer, a protest with a crowd - this is the same call with a simpler scene: the point
# is that nothing is lost from the callout list when the other packs are uninstalled, not that the
# staging is identical.

# --- 686 Callouts, its thirty
Add @{ id='p686-alleyway-robbery'; for='lspd'; name='Alleyway Robbery'; msg='Robbery in progress in an alley, caller watching from a window.'; adv='One male, knife seen, victim still there.'; line='Knife, one male, and the victim is still in the alley with him.'; app='Flee'; armed=$true; weapon='WEAPON_KNIFE'; amb=$true }
Add @{ id='p686-area-surveillance'; for='lspd'; name='Area Surveillance'; msg='Request for surveillance of an area after repeated reports.'; adv='No crime in progress, several complaints logged.'; line='Nothing to attend as such - show yourself in the area and see what is going on.'; res='Manual' }
Add @{ id='p686-attempted-break-in'; for='lspd'; name='Attempted Break-In'; msg='Attempted break-in reported, offender disturbed.'; adv='Rear window damaged, offender gone on foot.'; line='They were disturbed and they ran. The window is damaged but nobody is inside.'; app='Flee' }
Add @{ id='p686-car-bomb'; for='lspd,fire'; name='Suspected Vehicle Bomb'; msg='Vehicle reported with wiring visible under the dashboard.'; adv='Nobody in or near the vehicle, caller keeping people back.'; line='Wiring under the dash and nobody near it. Do not touch the car - keep the street clear.'; res='Manual'; backup=$true }
Add @{ id='p686-custom-pursuit'; for='lspd'; name='Pursuit in Progress'; msg='Vehicle failing to stop, pursuit not yet authorised.'; adv='Vehicle heading west at speed, driver not identified.'; line='They are not stopping and they are going west. Call it in before you commit.'; app='FleeInVehicle'; vehicle='buffalo' }
Add @{ id='p686-disorientated-individual'; for='lspd,ems'; name='Disorientated Individual'; msg='Person wandering in the road, disorientated.'; adv='No weapon seen, person cannot say where they live.'; line='He is in the road and he does not know where he is. Could be medical, could be drink.'; res='Manual'; amb=$true }
Add @{ id='p686-fare-dodger'; for='lspd'; name='Fare Evader'; msg='Passenger refusing to pay and refusing to leave a bus.'; adv='Driver holding the bus at a stop, passenger on board.'; line='The bus is stopped and he will not get off it. Driver wants him dealt with, not arrested.'; app='Flee' }
Add @{ id='p686-high-risk-escort'; for='lspd,sheriff'; name='High Risk Escort'; msg='Escort required for a high-risk transfer.'; adv='Transfer waiting on you, route already cleared.'; line='They will not move until you are with them. It is a straight run and they want it done quietly.'; res='Manual'; backup=$true }
Add @{ id='p686-kidnapping'; for='lspd'; name='Kidnapping in Progress'; msg='Person forced into a vehicle, caller following at a distance.'; adv='Two suspects, victim in the back seat, vehicle still moving.'; line='They put somebody in the back of it and drove. The caller is behind them and staying on the line.'; app='FleeInVehicle'; armed=$true; count=2; backup=$true }
Add @{ id='p686-lane-closure'; for='sahp'; name='Lane Closure'; msg='Request to close a lane for a recovery.'; adv='Recovery truck on scene, traffic down to one lane.'; line='The truck is there and the traffic is backing up. Somebody needs to stand in the road.'; res='Manual' }
Add @{ id='p686-large-vehicle-pursuit'; for='lspd,sahp'; name='Large Vehicle Failing to Stop'; msg='HGV failing to stop, driver ignoring lights.'; adv='Vehicle continuing at speed, load insecure.'; line='He is not stopping and there is a load on the back of it. Keep your distance.'; app='FleeInVehicle'; vehicle='phantom' }
Add @{ id='p686-offensive-weapon'; for='lspd'; name='Offensive Weapon Reported'; msg='Person reported carrying a weapon in public.'; adv='Male on foot, bat or club seen, no victims.'; line='Bat in his hand and no victims yet. He is walking, not running.'; app='HandsUp'; armed=$true; weapon='WEAPON_BAT' }
Add @{ id='p686-photography'; for='lspd'; name='Photographing a Police Station'; msg='Person photographing a police station, reported by staff.'; adv='Male on foot with a camera, no offence reported.'; line='Nothing has happened, he is just taking pictures of the building. Staff want him spoken to.'; res='Manual' }
Add @{ id='p686-property-checkup'; for='lspd,sheriff'; name='Property Check'; msg='Alarm activated at a property, no answer from the keyholder.'; adv='No persons seen, no forced entry visible.'; line='Alarm has been going ten minutes and the keyholder is not answering. Could be nothing.'; res='Manual' }
Add @{ id='p686-protest'; for='lspd'; name='Protest Reported'; msg='Protest gathering outside a building.'; adv='Around twenty people, no violence reported.'; line='Loud but peaceful so far. Show a presence and keep the road open.'; res='Manual'; backup=$true }
Add @{ id='p686-solicitation'; for='lspd'; name='Solicitation Reported'; msg='Solicitation reported on a street corner.'; adv='Female on foot, no violence, businesses complaining.'; line='Businesses have had enough. Nobody is in danger, they just want her moved on.'; res='Manual' }
Add @{ id='p686-sting-operation'; for='lspd'; name='Sting Operation'; msg='Sting in progress, suspects about to arrive.'; adv='Two vehicles expected, plain-clothes officers already in place.'; line='Everybody is in position and they are expecting two vehicles. Do not roll up in front of them.'; app='Hostile'; armed=$true; count=2; backup=$true }
Add @{ id='p686-stolen-item'; for='lspd'; name='Stolen Item Reported'; msg='Stolen item seen in a pawn shop window.'; adv='Staff cooperating, item matches a recent report.'; line='It matches the description exactly and the staff are being helpful about it.'; res='Manual' }
Add @{ id='p686-stolen-pedal-bike'; for='lspd'; name='Stolen Pedal Cycle'; msg='Bicycle stolen from outside a shop minutes ago.'; adv='Suspect rode off towards the park, description given.'; line='He rode off on it towards the park five minutes ago. He will not have gone far.'; app='Flee' }
Add @{ id='p686-subway-disturbance'; for='lspd'; name='Subway Disturbance'; msg='Disturbance on a subway platform.'; adv='Two males arguing, staff asking for someone to attend.'; line='Two of them on the platform and the staff have had enough. It has not turned violent.'; app='Hostile' }
Add @{ id='p686-suspected-stalker'; for='lspd'; name='Suspected Stalker'; msg='Person repeatedly seen watching an address.'; adv='Male in a parked car, no offence witnessed yet.'; line='Third report this week, same car, same spot. He is there now.'; res='Manual' }
Add @{ id='p686-swat-raid'; for='lspd,swat'; name='SWAT Raid'; msg='Raid authorised on an address, occupants refusing to come out.'; adv='Two inside, weapons believed present, entry team waiting on your call.'; line='They will not come out and the entry team is waiting on you. Two inside, and they are armed.'; app='Hostile'; armed=$true; count=2; backup=$true }
Add @{ id='p686-terrorist-attack'; for='any'; name='Terrorist Attack'; msg='Attack in progress, multiple callers, shots reported.'; adv='Several armed, casualties reported, area not secured.'; line='Multiple callers, shots, and casualties. Nobody has secured anything yet - go in carefully.'; app='Hostile'; armed=$true; count=3; backup=$true; amb=$true }
Add @{ id='p686-traffic-stop-backup'; for='lspd,sahp,sheriff'; name='Traffic Stop Backup'; msg='Officer requesting backup at a traffic stop.'; adv='Single officer on scene, driver not complying.'; line='One officer on scene and the driver is not doing what he is told. Get there.'; res='Manual'; backup=$true }
Add @{ id='p686-vehicle-on-sidewalk'; for='lspd'; name='Vehicle on the Pavement'; msg='Vehicle driven onto the pavement, pedestrians having to step into the road.'; adv='Driver still with the vehicle, no contact made.'; line='He has parked it across the pavement and he is still sat in it.'; res='Manual' }
Add @{ id='p686-vip-evacuation'; for='lspd'; name='VIP Evacuation'; msg='Protective detail requesting an evacuation route.'; adv='Principal in a vehicle, route not yet clear.'; line='They need the route clear before they move. It is a two-minute job if the road is empty.'; res='Manual'; backup=$true }
Add @{ id='p686-aircraft-in-distress'; for='any'; name='Aircraft in Distress'; msg='Light aircraft reported in difficulty, may be attempting to land.'; adv='Smoke reported from the engine, pilot still in contact.'; line='Smoke from the engine and he is trying to put it down. Wherever he lands, we are going.'; res='Manual'; amb=$true }

# --- United Callouts, its enabled nineteen
Add @{ id='uc-stolen-emergency-vehicle'; for='lspd,sheriff'; name='Stolen Emergency Vehicle'; msg='Ambulance stolen from outside a hospital.'; adv='Vehicle heading north, driver in a uniform.'; line='Somebody has taken an ambulance from the hospital bay. Blue lights, no crew.'; app='FleeInVehicle'; vehicle='ambulance' }
Add @{ id='uc-money-truck-theft'; for='lspd'; name='Money Truck Theft'; msg='Armoured truck being broken into, guards held.'; adv='Two males, both armed, truck stopped.'; line='They have stopped the truck and they are working on the back of it. Two, both armed.'; app='Hostile'; armed=$true; count=2 }
Add @{ id='uc-stolen-bus'; for='lspd'; name='Stolen Bus'; msg='Bus taken from a depot, driver refusing to stop.'; adv='Bus heading through the city, no passengers on board.'; line='He took it empty, so there is nobody on board to worry about. He is still going.'; app='FleeInVehicle'; vehicle='bus' }
Add @{ id='uc-stolen-commercial-vehicle'; for='lspd,sheriff'; name='Stolen Commercial Vehicle'; msg='Flatbed truck taken from a yard.'; adv='Vehicle loaded with plant equipment, heading out of the city.'; line='He took the truck with the equipment still on the back. He will be heading out of town.'; app='FleeInVehicle'; vehicle='flatbed' }
Add @{ id='uc-gang-shootout'; for='lspd'; name='Gang Shootout'; msg='Shots exchanged between two groups.'; adv='Four or more involved, at least one injured.'; line='Two groups and they are shooting at each other. Wait for back-up if you can.'; app='Hostile'; armed=$true; count=4; backup=$true; amb=$true }
Add @{ id='uc-armed-clown'; for='lspd'; name='Person in Clown Mask with a Weapon'; msg='Person in a mask carrying a weapon, frightening the public.'; adv='Male in a clown mask, weapon visible, no victims.'; line='He is in a mask and he has something in his hand, and he is enjoying the reaction.'; app='Hostile'; armed=$true; weapon='WEAPON_BAT' }
Add @{ id='uc-person-with-knife'; for='lspd'; name='Person with a Knife'; msg='Person in the street with a knife, public backing away.'; adv='Male on foot, knife in hand, no victims yet.'; line='Knife in his hand and people are backing away from him. Nobody has been hurt yet.'; app='Hostile'; armed=$true; weapon='WEAPON_KNIFE' }
Add @{ id='uc-warrant-for-arrest'; for='lspd,sheriff'; name='Warrant for Arrest'; msg='Wanted person believed at an address.'; adv='Name and description confirmed, occupant uncooperative.'; line='Address confirmed and the warrant is live. Nobody has answered the door but somebody is in.'; res='Manual'; backup=$true }
Add @{ id='uc-metro-disturbance'; for='lspd'; name='Disturbance at a Metro Station'; msg='Disturbance at a metro station, staff asking for attendance.'; adv='Three involved, no weapons seen.'; line='Three of them and the staff want them out. No weapons mentioned.'; app='Hostile'; count=3 }
Add @{ id='uc-cyclist-motorway'; for='sahp'; name='Cyclist on the Highway'; msg='Cyclist riding on the highway, drivers reporting near misses.'; adv='Single cyclist in a live lane, no lights.'; line='He is in a live lane with no lights on him. Somebody is going to hit him.'; res='Manual' }
Add @{ id='uc-atm-activity'; for='lspd'; name='Suspicious Activity at an ATM'; msg='People loitering around an ATM with tools.'; adv='Two males, tools visible, no offence seen yet.'; line='Two of them and something long in a bag. They have not touched the machine yet.'; app='Flee'; count=2 }
Add @{ id='uc-k9-backup'; for='lspd,sheriff'; name='K9 Backup Required'; msg='Handler requesting a K9 unit, suspect fled on foot.'; adv='Suspect in a yard, no weapon seen, handler holding the perimeter.'; line='He is in the yard and the handler wants a dog rather than a foot chase. Hold what you have.'; res='Manual'; backup=$true }

# --- the story-mission remakes, as police work
Add @{ id='story-jewel-store-job'; for='lspd'; name='Jewellery Store Robbery'; msg='Smash and grab at a jewellery store, alarm activated.'; adv='Three suspects, mopeds waiting outside.'; line='They are in the shop and there are bikes waiting outside. Nobody has stopped them yet.'; app='Flee'; armed=$true; count=3 }
Add @{ id='story-armoured-truck'; for='lspd'; name='Armoured Truck Intercepted'; msg='Armoured truck stopped in the road, crew not responding.'; adv='Truck abandoned, crew unaccounted for.'; line='The truck is stopped and nobody is answering from it. Approach like they are still inside.'; app='Hostile'; armed=$true; backup=$true }
Add @{ id='story-hood-safari'; for='sheriff'; name='Hunters Reported'; msg='Armed men in the county, residents reporting shots.'; adv='Two vehicles, rifles seen, one resident confronted.'; line='They have rifles and one of them has already had a go at a resident. Both vehicles are still close.'; app='Hostile'; armed=$true; weapon='WEAPON_RIFLE'; count=2; backup=$true }
Add @{ id='story-bank-heist-aftermath'; for='lspd'; name='Heist Aftermath'; msg='Bank robbery abandoned, suspects fleeing on foot.'; adv='Cash on the pavement, three suspects running.'; line='They have dropped half of it and they are running. The money is not the priority.'; app='Flee'; armed=$true; count=3; backup=$true }

# ============================================================ More of everything
#
# The second half of the library: more situations for every duty, so that a patrol of any kind has
# enough variety that the same call does not come round twice in a shift.

# --- LSPD, city work
Add @{ id='lspd-mugging'; for='lspd'; name='Street Robbery'; msg='Street robbery just happened, victim still on scene.'; adv='Two males on foot, phone and wallet taken.'; line='Two of them, took his phone and ran. He is still there and he is shaken.'; app='Flee'; count=2; amb=$true }
Add @{ id='lspd-pickpocket'; for='lspd'; name='Pickpocket Reported'; msg='Pickpocket seen working a crowd.'; adv='One male, hands in and out of pockets, no violence.'; line='He is working the queue outside the station. Nobody has noticed except the caller.'; app='Flee' }
Add @{ id='lspd-copper-theft'; for='lspd'; name='Cable Theft in Progress'; msg='Person cutting cable from a substation.'; adv='One male with tools, no power lines down.'; line='He is cutting the cable off the wall. Do not touch the metalwork.' }
Add @{ id='lspd-shoplifting-crew'; for='lspd'; name='Organised Shoplifting'; msg='Group filling bags and walking out of a store.'; adv='Three of them, staff told not to intervene.'; line='Three of them, bags full, and the staff have been told not to get involved. Which leaves it to you.'; app='Flee'; count=3 }
Add @{ id='lspd-stolen-motorbike'; for='lspd'; name='Stolen Motorcycle'; msg='Motorcycle taken from outside a cafe minutes ago.'; adv='Two riders, both in dark clothing, no plates.'; line='Two on it, no plates, gone towards the freeway. They will not stop for you.'; app='FleeInVehicle'; vehicle='bati' }
Add @{ id='lspd-catalytic-theft'; for='lspd'; name='Catalytic Converter Theft'; msg='Person under a parked car with a saw.'; adv='One male, car jacked up, no owner present.'; line='He has it up on a jack and he is under it with a saw. Owner is not coming.'; app='Flee' }
Add @{ id='lspd-car-meet'; for='lspd,sahp'; name='Car Meet Nuisance'; msg='Large car meet blocking a retail park.'; adv='Around fifteen vehicles, engines revving, no racing yet.'; line='Fifteen cars and nobody is moving. They will scatter the moment you arrive - which is half the problem.'; res='Manual'; backup=$true }
Add @{ id='lspd-illegal-vendor'; for='lspd'; name='Illegal Street Vendor'; msg='Unlicensed vendor blocking the pavement.'; adv='One male, stall set up, no violence.'; line='Pavement blocked, no licence, and he has been told twice already.'; res='Manual' }
Add @{ id='lspd-public-intoxication'; for='lspd'; name='Public Intoxication'; msg='Person asleep in a doorway, very drunk.'; adv='Male, no injuries seen, businesses complaining.'; line='He is asleep in the doorway and he smells like a brewery. Do not let him sleep it off in this weather.'; res='Manual'; amb=$true }
Add @{ id='lspd-indecent-exposure'; for='lspd'; name='Indecent Exposure'; msg='Person reported exposing themselves in a park.'; adv='Male on foot, no other parties involved.'; line='In the park, on his own, and the caller is not making it up. Go and deal with him.'; app='Flee' }
Add @{ id='lspd-missing-child'; for='lspd'; name='Missing Child'; msg='Child reported missing from a shopping centre.'; adv='Seven years old, last seen ten minutes ago, mother with staff.'; line='Seven years old and missing ten minutes in a shopping centre. Staff are checking cameras now.'; res='Manual'; backup=$true; amb=$true }
Add @{ id='lspd-child-neglect'; for='lspd'; name='Child Welfare Concern'; msg='Children left alone at an address, neighbour concerned.'; adv='Two children, no adults seen for hours.'; line='Nobody has been in or out for most of the day and the children are on their own. The neighbour is sure.'; res='Manual'; backup=$true }
Add @{ id='lspd-elder-abuse'; for='lspd'; name='Concern for an Elderly Resident'; msg='Care worker reporting bruising on an elderly client.'; adv='Client on scene, carer present, no violence reported.'; line='The care worker is describing bruising nobody has explained. Take it seriously and take your time.'; res='Manual'; amb=$true }
Add @{ id='lspd-illegal-firearm-sale'; for='lspd'; name='Firearm Sale Reported'; msg='Two males exchanging a weapon in a car park.'; adv='One weapon seen, in a bag, no shots fired.'; line='A bag, a gun and money changing hands. Nobody has fired anything yet.'; app='Flee'; armed=$true; count=2 }
Add @{ id='lspd-gang-intimidation'; for='lspd'; name='Gang Intimidation'; msg='Group intimidating a shop owner on his doorstep.'; adv='Three males, no weapons seen, shop owner frightened.'; line='Three of them at his door and he is on his own inside. Nobody has been hit yet.'; app='Hostile'; count=3 }
Add @{ id='lspd-taxi-assault'; for='lspd'; name='Assault on a Driver'; msg='Taxi driver assaulted by a passenger.'; adv='Driver on scene with a facial injury, suspect gone on foot.'; line='Passenger has gone and the driver has taken one to the face over a fare.'; app='Flee'; amb=$true }
Add @{ id='lspd-bus-assault'; for='lspd'; name='Assault on a Bus'; msg='Passenger assaulted on a bus, driver holding the vehicle.'; adv='Suspect on board, victim with staff, no weapon seen.'; line='The driver has stopped the bus and the suspect is still on it. Victim is with the other passengers.'; res='Manual'; amb=$true }
Add @{ id='lspd-vandalism-school'; for='lspd'; name='Vandalism at a School'; msg='Windows broken at a school overnight, caretaker reporting.'; adv='Nobody on scene, damage to four windows.'; line='Four windows and nobody about. The caretaker will meet you there.'; res='Manual' }
Add @{ id='lspd-fireworks'; for='lspd'; name='Fireworks Complaint'; msg='Fireworks being set off in a residential street.'; adv='Group of four, fireworks in a bin, no injuries.'; line='Four of them letting them off down the middle of the road. Somebody will lose a hand at this rate.'; app='Flee'; count=4 }
Add @{ id='lspd-false-alarm'; for='lspd'; name='Alarm Activation'; msg='Commercial alarm activated, keyholder on the way.'; adv='No persons seen, building secure from the front.'; line='Probably nothing - but the last three at this address were not.'; res='Manual' }
Add @{ id='lspd-drone-over-prison'; for='lspd,prison'; name='Drone Over the Perimeter'; msg='Drone seen over a secure perimeter.'; adv='Drone still airborne, operator believed nearby.'; line='Somebody is flying a drone over the fence and they are not doing it for the view.'; app='Flee'; backup=$true }

# --- Sheriff, county
Add @{ id='lssd-barn-fire'; for='sheriff,fire'; name='Barn Fire'; msg='Barn alight on farmland, livestock inside.'; adv='Owner on scene, animals still inside, no persons hurt.'; line='The barn is well alight and there are animals in it. The owner is trying to get them out himself.'; app='Fire'; amb=$true }
Add @{ id='lssd-horse-loose'; for='sheriff'; name='Horse Loose on the Road'; msg='Horse loose on a county road, drivers swerving.'; adv='Animal in the road, owner looking for it.'; line='It is in the road and drivers are swerving round it. The owner is out looking.'; res='Manual' }
Add @{ id='lssd-firearms-stolen'; for='sheriff'; name='Firearms Stolen from a Farm'; msg='Shotgun cabinet forced at a farmhouse.'; adv='Two weapons taken, owners were out.'; line='Cabinet forced, two gone. Whoever did it knew when they would be out.'; res='Manual'; backup=$true }
Add @{ id='lssd-trailer-meth'; for='sheriff'; name='Suspicious Trailer'; msg='Trailer on a back road with a chemical smell.'; adv='No persons seen, generator running.'; line='Generator going and a smell that is not farmyard. Nobody about.'; backup=$true }
Add @{ id='lssd-domestic-firearms'; for='sheriff'; name='Domestic with Firearms'; msg='Domestic incident, caller says there are guns in the house.'; adv='One male inside, family outside with a neighbour.'; line='The family are out and they say there are guns in the house. He is still in there.'; app='Hostile'; armed=$true; backup=$true; amb=$true }
Add @{ id='lssd-ranch-hand-missing'; for='sheriff'; name='Missing Ranch Hand'; msg='Ranch hand not returned from checking fences.'; adv='On foot, no phone signal in the area.'; line='He went out to check fences this morning and he has not come back. No signal out there.'; res='Manual'; amb=$true }
Add @{ id='lssd-illegal-logging'; for='sheriff,ranger'; name='Illegal Logging'; msg='Chainsaws and a flatbed working in protected woodland.'; adv='Two vehicles, equipment running, no persons confronted.'; line='Two trucks and chainsaws going. They are not supposed to be in there at all.'; app='Flee'; backup=$true }
Add @{ id='lssd-hunter-shot'; for='sheriff,ems'; name='Hunter Injured'; msg='Hunter injured in the field, another hunter on the phone.'; adv='Gunshot wound to the leg, conscious, remote location.'; line='Shot in the leg and he is a long way from a road. The ambulance is not going to reach him on its own.'; res='Manual'; amb=$true }
Add @{ id='lssd-livestock-attack'; for='sheriff,ranger'; name='Livestock Attacked'; msg='Sheep attacked overnight, predator believed still nearby.'; adv='Several dead, owner asking for someone to attend.'; line='Something has been through the flock and the owner thinks it is still about.'; res='Manual' }
Add @{ id='lssd-hunters-trespass'; for='sheriff'; name='Hunters on Private Land'; msg='Two hunters with rifles on land they have no permission for.'; adv='Both on foot, rifles slung, landowner confronting them.'; line='They are on his land with rifles and he is out there arguing with them. Get there before he loses his temper.'; app='Hostile'; armed=$true; weapon='WEAPON_RIFLE'; count=2 }
Add @{ id='lssd-still-fire'; for='sheriff,fire'; name='Fire at a Remote Property'; msg='Fire at an outbuilding on a remote property.'; adv='No persons seen, smoke visible from the road.'; line='Smoke you can see from the highway and nobody answering. It could be the still going up.'; app='Fire' }

# --- Highway Patrol
Add @{ id='sahp-motorcycle-racing'; for='sahp'; name='Motorcycle Racing'; msg='Motorcycles racing on the highway.'; adv='Three bikes, no plates, weaving between traffic.'; line='Three of them between the lanes and none of them with plates. Do not try to match them.'; app='FleeInVehicle'; vehicle='bati'; count=3 }
Add @{ id='sahp-overweight-lorry'; for='sahp'; name='Overweight Lorry'; msg='Lorry stopped for a weight check, driver abusive.'; adv='Vehicle on the weighbridge, driver out of the cab.'; line='He is out of the cab and he is not interested in the weighbridge. The paperwork is probably worse than the weight.'; app='Hostile' }
Add @{ id='sahp-debris'; for='sahp'; name='Debris in the Road'; msg='Debris across two lanes, source unknown.'; adv='No vehicles stopped nearby, several near misses.'; line='Nobody knows where it came from and three people have nearly hit it. Somebody has to stand there.'; res='Manual' }
Add @{ id='sahp-jackknife'; for='sahp'; name='Jackknifed Lorry'; msg='Articulated lorry jackknifed across the carriageway.'; adv='Driver out and unhurt, carriageway blocked both ways.'; line='It is across both lanes and the driver is out. Nobody is hurt, everything is stuck.'; res='Manual'; amb=$true }
Add @{ id='sahp-cow-motorway'; for='sahp,sheriff'; name='Animal on the Carriageway'; msg='Cow on the carriageway, drivers stopping.'; adv='Animal wandering, no owner present.'; line='There is a cow on the carriageway and drivers are stopping to film it. Clear the traffic first.'; res='Manual' }
Add @{ id='sahp-cyclist-collision'; for='sahp,ems'; name='Cyclist Collision'; msg='Cyclist struck by a car, driver on scene.'; adv='Casualty in the road, driver cooperating, lane blocked.'; line='Cyclist down and the driver is with him, and he is not moving his arm.'; patient='a_m_y_skater_01'; seconds=16; pline='My shoulder. I went over the bonnet.' }
Add @{ id='sahp-roadworks'; for='sahp'; name='Incident at Roadworks'; msg='Vehicle has gone through a roadworks closure.'; adv='No injuries, cones scattered, workers shaken.'; line='Nobody is hurt, which is remarkable. The workers want him found.'; app='FleeInVehicle'; vehicle='dilettante' }
Add @{ id='sahp-tunnel-fire'; for='fire,sahp'; name='Vehicle Fire in a Tunnel'; msg='Vehicle alight inside a tunnel, traffic still moving.'; adv='Occupants out, smoke filling the tunnel.'; line='They are out of it but the smoke has nowhere to go and people are still driving in.'; app='Fire'; amb=$true }

# --- Rangers
Add @{ id='sapr-bear-sighting'; for='ranger'; name='Bear Sighting'; msg='Bear reported near a campsite, campers concerned.'; adv='Animal not aggressive, campers still on site.'; line='It has not gone for anybody yet. Move the campers on before it changes its mind.'; res='Manual' }
Add @{ id='sapr-campers-fire'; for='ranger,fire'; name='Illegal Fire at a Campsite'; msg='Large fire at a campsite in a restricted area.'; adv='Six campers, fire well established, no injuries.'; line='Six of them around a fire that is far bigger than the rules allow. Wind is coming up.'; app='Fire' }
Add @{ id='sapr-illegal-fishing'; for='ranger'; name='Illegal Fishing'; msg='People netting a protected lake.'; adv='Two males with nets and a cooler, vehicle nearby.'; line='Nets in a lake they should not be anywhere near. Vehicle is parked on the trail.'; app='Flee'; count=2 }
Add @{ id='sapr-climber-stuck'; for='ranger,ems'; name='Climber Stuck on a Ledge'; msg='Climber unable to move, below the ridge.'; adv='Conscious, one arm injured, no way down alone.'; line='She is on a ledge and she cannot get off it. There is no road within a mile of her.'; res='Manual'; amb=$true }
Add @{ id='sapr-tourist-lost'; for='ranger'; name='Tourists Lost on a Trail'; msg='Family of four lost on a trail, phone at 4% battery.'; adv='They can describe a waterfall and a fence.'; line='They have a waterfall and a fence and about four per cent of battery left.'; res='Manual'; amb=$true }
Add @{ id='sapr-snake-bite'; for='ranger,ems'; name='Snake Bite'; msg='Walker bitten by a snake on a trail.'; adv='Conscious, bite to the calf, remote location.'; line='Bitten on the leg and a long way from the road. Keep him still and get to him.'; patient='a_m_y_hipster_01'; seconds=14; pline='It is going numb. Is that normal?' }

# --- Prison
Add @{ id='saspa-drone-drop'; for='prison'; name='Drone Drop'; msg='Drone seen dropping a package inside the perimeter.'; adv='Package recovered, drone gone.'; line='It came over the wall and dropped it. The drone is gone and the package is ours.'; res='Manual' }
Add @{ id='saspa-escape-vehicle'; for='prison,sheriff'; name='Escape Vehicle Reported'; msg='Vehicle seen taking on a passenger at the perimeter road.'; adv='Vehicle heading for the highway, one passenger boarded.'; line='Somebody got into it at the fence line and it went for the highway. That is an escape attempt in progress.'; app='FleeInVehicle'; vehicle='premier'; backup=$true }
Add @{ id='saspa-visitor-contraband'; for='prison'; name='Contraband with a Visitor'; msg='Visitor found with contraband at the gate.'; adv='Visitor detained, item recovered, no violence.'; line='They found it at the gate and she is still in the search room. She is not saying who it was for.'; res='Manual' }
Add @{ id='saspa-medical-emergency'; for='prison,ems'; name='Medical Emergency - Inmate'; msg='Inmate collapsed in a workshop.'; adv='Conscious but unwell, staff on scene, no violence.'; line='He went down in the workshop and the staff have him sitting up. They want the ambulance and so do I.'; patient='s_m_m_prisguard_01'; seconds=14; pline='I came over dizzy. I have not felt right since breakfast.' }

# --- EMS, more patients
Add @{ id='ems-stroke'; for='ems'; name='Suspected Stroke'; msg='Person with slurred speech and a weak side.'; adv='Conscious, family present, onset twenty minutes ago.'; line='Face dropped and one side weak, twenty minutes ago. That is a stroke clock and it is running.'; patient='a_m_m_business_01'; seconds=14; pline='My arm went heavy. I could not hold the phone.' }
Add @{ id='ems-allergic-reaction'; for='ems'; name='Allergic Reaction'; msg='Person swollen after eating, struggling to swallow.'; adv='Conscious, epinephrine not used, restaurant staff on scene.'; line='Swelling fast and she has not used a pen. This is the one that goes wrong if nobody gets there.'; patient='a_f_y_vinewood_01'; seconds=12; pline='It is going down. I can swallow again.' }
Add @{ id='ems-burn-injury'; for='ems,fire'; name='Burn Injury'; msg='Person with burns to the arms from a kitchen.'; adv='Conscious, burns to both forearms, kitchen staff on scene.'; line='Both forearms and he is holding them out like he cannot decide what to do. Cool it and get him moving.'; patient='a_m_y_business_02'; seconds=15; pline='I poured it over myself. It was on the stove.' }
Add @{ id='ems-industrial-accident'; for='ems'; name='Industrial Accident'; msg='Worker injured by machinery at a depot.'; adv='Conscious, hand caught, machinery stopped.'; line='His hand was in it when it went. The machinery is stopped and so is the bleeding, for now.'; patient='a_m_y_construct_02'; seconds=18; pline='I cannot see how bad it is. Tell me how bad it is.' }
Add @{ id='ems-dog-bite'; for='ems'; name='Dog Bite'; msg='Person bitten by a dog, wound to the leg.'; adv='Conscious, dog secured by the owner, wound bleeding.'; line='Dog is secured and the owner is apologetic, which does not help the leg.'; patient='a_f_y_business_01'; seconds=12; pline='It just went for me. I did not even touch it.' }
Add @{ id='ems-drowning'; for='ems,ranger'; name='Near Drowning'; msg='Person pulled from the water, not breathing.'; adv='Bystanders started compressions, water still in the lungs.'; line='They got her out and somebody is doing compressions. Do not stop until you have to.'; patient='a_m_y_beach_03'; seconds=20; pline='(a cough, then breathing)' }
Add @{ id='ems-electric-shock'; for='ems'; name='Electric Shock'; msg='Person shocked by a live cable at a site.'; adv='Power isolated, casualty conscious, burns to the hand.'; line='They have killed the power and he is conscious, with entry and exit burns on the one hand.'; patient='a_m_y_construct_01'; seconds=15; pline='My hand is buzzing. It will not stop buzzing.' }
Add @{ id='ems-birth'; for='ems'; name='Birth Imminent'; msg='Woman in labour, no time for the hospital.'; adv='Conscious, partner present, contractions close.'; line='There is no time for the hospital and she knows it. You are the closest thing to a professional.'; patient='a_f_y_bevhills_01'; seconds=20; pline='Is she all right? Tell me she is all right.' }
Add @{ id='ems-withdrawal'; for='ems'; name='Withdrawal'; msg='Person shaking and vomiting, withdrawing from something.'; adv='Conscious, no injuries, refuses to say what.'; line='Shaking, sick, and not saying what he has been taking. Keep your kit close.'; patient='a_m_m_tramp_01'; seconds=14; pline='I just need to get through it. I have done it before.' }
Add @{ id='ems-psychiatric'; for='ems'; name='Psychiatric Emergency'; msg='Person in acute distress, family asking for help.'; adv='No weapon, no aggression, family on scene.'; line='Nobody has been hurt and nobody is threatening anybody. He is frightened and so is his family.'; res='Manual'; amb=$true }
Add @{ id='ems-glass-assault'; for='ems'; name='Assault with a Glass'; msg='Person cut in a bar, injury to the neck.'; adv='Conscious, bleeding heavily, suspect gone.'; line='Cut across the neck with a glass and the suspect has gone. Pressure on it and get him out of there.'; patient='a_m_y_business_01'; seconds=18; pline='It is not as bad as it looks. Is it as bad as it looks?' }
Add @{ id='ems-gunshot-wound'; for='ems,lspd'; name='Gunshot Wound'; msg='Person shot, conscious, suspect gone.'; adv='Wound to the shoulder, caller applying pressure.'; line='He has been shot in the shoulder and the caller is holding it. Suspect has gone - the patient comes first.'; patient='a_m_m_hasjew_01'; seconds=18; pline='I cannot move my arm. Tell me it is still there.' }
Add @{ id='ems-stab-wound'; for='ems,lspd'; name='Stab Wound'; msg='Person stabbed, wound to the abdomen.'; adv='Conscious, suspect gone, caller on scene.'; line='Stabbed in the stomach and still talking, which is not the same as being all right.'; patient='a_m_y_vinewood_01'; seconds=18; pline='It does not hurt as much as it should. Is that bad?' }
Add @{ id='ems-carbon-monoxide'; for='ems,fire'; name='Possible Carbon Monoxide'; msg='Family feeling unwell in a house, boiler suspected.'; adv='Four people, all drowsy and headachy.'; line='Four of them, all drowsy, all with headaches, and all in the same house. Get them out first.'; res='Manual'; amb=$true }
Add @{ id='ems-farm-machinery'; for='ems,sheriff'; name='Farm Machinery Injury'; msg='Person injured by a tractor, arm trapped.'; adv='Conscious, arm trapped, machine switched off.'; line='His arm is in it and the machine is off, and now it is a question of getting him free without making it worse.'; patient='a_m_m_farmer_01'; seconds=20; pline='Do not move it. Whatever you do, do not move it.' }
Add @{ id='ems-sports-injury'; for='ems'; name='Sports Injury'; msg='Player injured on a pitch, suspected leg fracture.'; adv='Conscious, leg deformed, coaches keeping people back.'; line='The leg is not straight and he is being very brave about it. Splint him and keep the crowd off.'; patient='a_m_y_musclbeac_01'; seconds=14; pline='I heard it crack. I am not walking off.' }
Add @{ id='ems-food-poisoning'; for='ems'; name='Multiple People Unwell'; msg='Several customers unwell at a restaurant.'; adv='Four people, all vomiting, same meal.'; line='Four of them, all the same meal, and the kitchen is still serving. Start with the worst.'; res='Manual'; amb=$true }

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
