module WaveSim

open Fulma
open Fable.React
open Fable.React.Props

open CommonTypes
open ModelType
open DiagramStyle
open WaveSimStyle
open WaveSimHelpers
open FileMenuView
open SimulatorTypes
open NumberHelpers
open DrawModelType
open Sheet.SheetInterface

// TODO: Move all Style definitions into Style.fs
// TODO: Combine Style definitions into same variables where possible

/// get string in the [x:x] format given the bit limits
let private bitLimsString (a, b) =
    match (a, b) with
    | (0, 0) -> ""
    | (msb, lsb) when msb = lsb -> sprintf "[%d]" msb
    | (msb, lsb) -> sprintf "[%d:%d]" msb lsb

let getInputName (comp: NetListComponent) (port: InputPortNumber) (fastSim: FastSimulation): string =
    match comp.Type with
    | ROM _ | RAM _ | AsyncROM _ ->
        failwithf "What? Legacy RAM component types should never occur"

    | Not | BusCompare _ ->
        let portName = ".IN"
        comp.Label + portName + bitLimsString (0, 0) 

    | And | Or | Xor | Nand | Nor | Xnor ->
        let portName = ".IN" + string port
        comp.Label + portName + bitLimsString (0, 0)

    | Mux2 ->
        let portName =
            match port with
            | CommonTypes.InputPortNumber 2 -> ".SEL"
            | _ -> "." + string port

        comp.Label + portName + bitLimsString (0, 0)

    | Mux4 ->
        let portName =
            match port with
            | CommonTypes.InputPortNumber 4 -> ".SEL"
            | _ -> "." + string port

        comp.Label + portName + bitLimsString (0, 0)

    | Mux8 ->
        let portName =
            match port with
            | CommonTypes.InputPortNumber 8 -> ".SEL"
            | _ -> "." + string port

        comp.Label + portName + bitLimsString (0, 0)

    | Decode4 ->
        let portName =
            match port with
            | CommonTypes.InputPortNumber 0 -> ".SEL"
            | _ -> ".DATA"

        comp.Label + portName + bitLimsString (0, 0)

    | Output w ->
        printf "output %A" comp.Label
        comp.Label + bitLimsString (w - 1, 0)

    | Input w | Output w | Constant1 (w, _, _) | Constant (w, _) | Viewer w ->
        comp.Label + bitLimsString (w - 1, 0)

    | Demux2 | Demux4 | Demux8 ->
        let portName =
            match port with
            | CommonTypes.InputPortNumber 0 -> ".DATA"
            | _ -> ".SEL"
        comp.Label + "." + portName + bitLimsString (0, 0)

    | NbitsXor w ->
        let portName =
            match port with
            | CommonTypes.InputPortNumber 0 -> ".P"
            | _ -> ".Q"
        comp.Label + portName + bitLimsString (w - 1, 0)

    | NbitsAdder w ->
        let portName =
            match port with
            | CommonTypes.InputPortNumber 0 -> ".Cin"
            | CommonTypes.InputPortNumber 1 -> ".P"
            | _ -> ".Q"
        comp.Label + portName + bitLimsString (w - 1, 0)

    | DFF | Register _ ->
        comp.Label + ".D" + bitLimsString (0, 0)

    | DFFE | RegisterE _ ->
        let portName =
            match port with
            | CommonTypes.InputPortNumber 0 -> ".D"
            | _ -> ".EN"
        comp.Label + portName + bitLimsString (0, 0)

    | ROM1 _ | AsyncROM1 _ ->
        comp.Label + ".ADDR"

    | RAM1 _ | AsyncRAM1 _ ->
        let portName =
            match port with
            | CommonTypes.InputPortNumber 0 -> ".ADDR"
            | CommonTypes.InputPortNumber 1 -> ".DIN"
            | _ -> ".WEN"
        comp.Label + portName

    | Custom c ->
        comp.Label + "." + fst c.InputLabels[getInputPortNumber port] + bitLimsString (snd c.InputLabels[getInputPortNumber port] - 1, 0)

    | IOLabel -> "input iolabel"
    | MergeWires -> "mergewires"
    | SplitWire _ -> "splitwire"
    | BusSelection _ -> "bus select"

let getOutputName (comp: NetListComponent) (port: OutputPortNumber) (fastSim: FastSimulation): string =
    match comp.Type with
    | ROM _ | RAM _ | AsyncROM _ ->
        failwithf "What? Legacy RAM component types should never occur"

    | Not | And | Or | Xor | Nand | Nor | Xnor | Decode4 | Mux2 | Mux4 | Mux8 | BusCompare _ ->
        comp.Label + ".OUT" + bitLimsString (0, 0)

    | Output w ->
        printf "output %A" comp.Label
        comp.Label + bitLimsString (w - 1, 0)


    | Input w | Output w | Constant1 (w, _, _) | Constant (w, _) | Viewer w ->
        comp.Label + bitLimsString (w - 1, 0)

    | Demux2 | Demux4 | Demux8 ->
        comp.Label + "." + string port + bitLimsString (0, 0)

    | NbitsXor w ->
        comp.Label + bitLimsString (w - 1, 0)

    | NbitsAdder w ->
        match port with
        | CommonTypes.OutputPortNumber 0 ->
            comp.Label + ".SUM" + bitLimsString (w - 1, 0)
        | _ ->
            comp.Label + ".COUT" + bitLimsString (w - 1, 0)

    | DFF | DFFE ->
        comp.Label + ".Q" + bitLimsString (0, 0)

    | Register w | RegisterE w ->
        comp.Label + ".Q" + bitLimsString (w - 1, 0)

    | RAM1 mem | AsyncRAM1 mem | AsyncROM1 mem | ROM1 mem ->
        comp.Label + ".DOUT" + bitLimsString (mem.WordWidth - 1, 0)

    | Custom c ->
        comp.Label + "." + fst c.OutputLabels[getOutputPortNumber port] + bitLimsString (snd c.OutputLabels[getOutputPortNumber port] - 1, 0)

    | IOLabel ->
        let drivingComp = fastSim.FIOActive[ComponentLabel comp.Label,[]]
        let labelWidth = FastRun.extractFastSimulationWidth fastSim (drivingComp.Id,[]) (CommonTypes.OutputPortNumber 0)
        match labelWidth with
        | None ->
            failwithf $"What? Can't find width for IOLabel {comp.Label}$ "
        | Some width ->
            comp.Label + bitLimsString (width - 1, 0)
    | MergeWires -> "mergewires"
    | SplitWire _ -> "splitwire"
    | BusSelection _ -> "bus select"

let getName (comp: NetListComponent) (port: PortNumber) (fastSim: FastSimulation): string =
    match port with
    | InputPortNumber ipn -> getInputName comp ipn fastSim
    | OutputPortNumber opn -> getOutputName comp opn fastSim

/// starting with just output ports only: not showing input ports.
let makeWave (fastSim: FastSimulation) (netList: Map<ComponentId, NetListComponent>) (index: WaveIndexT) (comp: NetListComponent) : Wave =
    let driverCompId, driverPort =
        match index.Port with
        | OutputPortNumber opn -> comp.Id, opn
        | InputPortNumber ipn ->
            match Map.tryFind ipn comp.Inputs with
            | Some (Some nlSource) -> nlSource.SourceCompId, nlSource.OutputPort
            | Some None -> failwithf "is there an unconnected input?\n wave: %A\n port: %A\n type: %A" comp.Label index.Port comp.Type
            | None -> failwithf "InputPortNumber %A not in comp.Inputs" ipn

    let driverComp = netList[driverCompId]

    //need to get driving one so needs to be an output
    let driverId, driverPort = getFastDriverNew fastSim driverComp driverPort

    let dispName = getName comp index.Port fastSim

    FastRun.runFastSimulation 500 fastSim
    let waveValues =
        [ 0 .. 500 ]
        |> List.map (fun i -> FastRun.extractFastSimulationOutput fastSim i driverId driverPort)

    {
        WaveId = index
        Type = comp.Type
        CompLabel = comp.Label
        Conns = []
        SheetId = []
        Driver = {DriverId = driverId; Port = driverPort}
        DisplayName = dispName
        Width =  getFastOutputWidth fastSim.FComps[driverId] driverPort
        WaveValues = waveValues
        Polylines = None
    }

let getWavesNew (simData: SimulationData) (reducedState: CanvasState) : Map<WaveIndexT, Wave> =
    let fastSim = simData.FastSim

    let netList = Helpers.getNetList reducedState

    let makeNewThing (thing: (ComponentId * NetListComponent)) : (WaveIndexT * NetListComponent) list =
        let compId = fst thing
        let nlc : NetListComponent = snd thing
        let inputNum = Map.count nlc.Inputs
        let outputNum = Map.count nlc.Outputs
        
        let outputs =
            [0 .. outputNum - 1]
            |> List.map (fun x -> 
                let num = CommonTypes.OutputPortNumber x
                {Id = compId; Port = OutputPortNumber num}, nlc
            )

        let inputs =
            match nlc.Type with
            | IOLabel -> []
            | _ ->
                [0 .. inputNum - 1]
                |> List.map (fun x ->
                    let num = CommonTypes.InputPortNumber x
                    {Id = compId; Port = InputPortNumber num}, nlc
                )

        List.append inputs outputs


    let newMap =
        netList
        |> Map.toList
        |> List.collect (makeNewThing)
        |> Map.ofList

    newMap
    |> Map.map (makeWave fastSim netList)

/// Generates SVG to display waveform values when there is enough space
let displayValuesOnWave (startCycle: int) (endCycle: int) (waveValues: WireData list) : ReactElement =
    // enough space means enough transitions such that the full value can be displayed before a transition occurs
    // values can be displayed repeatedly if there is enough space
    // try to centre the displayed values?
    failwithf "displayValuesOnWave not implemented"

/// Called when InitiateWaveSimulation msg is dispatched
/// Generates the polyline(s) for a specific waveform
let generateWaveform (wsModel: WaveSimModel) (index: WaveIndexT) (wave: Wave): Wave =
    let waveName = wave.DisplayName
    if List.contains index wsModel.SelectedWaves then
        // printf "generating wave for %A" waveName
        let polylines =
            match wave.Width with
            | 0 -> failwithf "Cannot have wave of width 0"
            | 1 ->
                let transitions = calculateBinaryTransitions wave.WaveValues
                /// TODO: Fix this so that it does not generate all 500 points.
                /// Currently takes in 0, but this should ideally only generate the points that
                /// are shown on screen, rather than all 500 cycles.
                let wavePoints =
                    List.mapi (binaryWavePoints wsModel.ZoomLevel 0) transitions 
                    |> List.concat
                    |> List.distinct

                [ polyline (wavePolylineStyle wavePoints) [] ]
            | _ ->
                let transitions = calculateNonBinaryTransitions wave.WaveValues
                /// TODO: Fix this so that it does not generate all 500 points.
                /// Currently takes in 0, but this should ideally only generate the points that
                /// are shown on screen, rather than all 500 cycles.
                let fstPoints, sndPoints =
                    List.mapi (nonBinaryWavePoints wsModel.ZoomLevel 0) transitions 
                    |> List.unzip
                let makePolyline points = 
                    let points =
                        points
                        |> List.concat
                        |> List.distinct
                    polyline (wavePolylineStyle points) []

                [makePolyline fstPoints; makePolyline sndPoints]

        {wave with Polylines = Some polylines}
    else wave

/// TODO: Test if this function actually works.
/// Displays error message if there is a simulation error
let displayErrorMessage error =
    div [ errorMessageStyle ]
        [ SimulationView.viewSimulationError error ]

/// Sets all waves as selected or not selected depending on value of newState
let toggleSelectAll (selected: bool) (wsModel: WaveSimModel) dispatch : unit =
    let selectedWaves = if selected then Map.keys wsModel.AllWaves |> Seq.toList else []
    dispatch <| InitiateWaveSimulation {wsModel with SelectedWaves = selectedWaves}
    // selectConns model conns dispatch

let selectAll (wsModel: WaveSimModel) dispatch =
    let allWavesSelected = Map.forall (fun index _ -> isWaveSelected wsModel index) wsModel.AllWaves

    tr summaryProps
        [
            th [] [
            Checkbox.checkbox []
                [ Checkbox.input [
                    Props 
                        (checkboxInputProps @ [
                            Checked allWavesSelected
                            OnChange(fun _ -> toggleSelectAll (not allWavesSelected) wsModel dispatch )
                    ])
                ] ]
            ]
            th [] [str "Select All"]
        ]
        

let toggleConnsSelect (index: WaveIndexT) (wsModel: WaveSimModel) (dispatch: Msg -> unit) =
    let selectedWaves =
        if List.contains index wsModel.SelectedWaves then
            List.except [index] wsModel.SelectedWaves
        else [index] @ wsModel.SelectedWaves

    let wsModel = {wsModel with SelectedWaves = selectedWaves}
    dispatch <| InitiateWaveSimulation wsModel
    // changeWaveSelection name model waveSimModel dispatch

/// TODO: Change name to editWaves
let closeWaveSimButton (wsModel: WaveSimModel) (dispatch: Msg -> unit) : ReactElement =
    let wsModel = {wsModel with State = WSClosed}
    button 
        [Button.Color IsSuccess; Button.Props [closeWaveSimButtonStyle]]
        (fun _ -> dispatch <| SetWSModel wsModel)
        (str "Close wave simulator")

/// Set highlighted clock cycle number
let private setClkCycle (wsModel: WaveSimModel) (dispatch: Msg -> unit) (newClkCycle: int) : unit =
    let newClkCycle = min Constants.maxLastClk newClkCycle |> max 0

    if newClkCycle <= endCycle wsModel then
        if newClkCycle < wsModel.StartCycle then
            printf "StartCycle: %A" newClkCycle
            dispatch <| InitiateWaveSimulation
                {wsModel with 
                    StartCycle = newClkCycle
                    CurrClkCycle = newClkCycle
                    ClkCycleBoxIsEmpty = false
                }
        else
            dispatch <| SetWSModel
                {wsModel with
                    CurrClkCycle = newClkCycle
                    ClkCycleBoxIsEmpty = false
                }
    else
        printf "StartCycle: %A" (newClkCycle - (wsModel.ShownCycles - 1))
        printf "CurrClkCycle: %A" newClkCycle
        dispatch <| InitiateWaveSimulation
            {wsModel with
                StartCycle = newClkCycle - (wsModel.ShownCycles - 1)
                CurrClkCycle = newClkCycle
                ClkCycleBoxIsEmpty = false
            }

let changeZoom (wsModel: WaveSimModel) (zoomIn: bool) (dispatch: Msg -> unit) = 
    let wantedZoomIndex =
        if zoomIn then wsModel.ZoomLevelIndex + 1
        else wsModel.ZoomLevelIndex - 1

    let newIndex, newZoom =
        Array.tryItem wantedZoomIndex Constants.zoomLevels
        |> function
            | Some zoom -> wantedZoomIndex, zoom
            // Index out of range: keep original zoom level
            | None -> wsModel.ZoomLevelIndex, wsModel.ZoomLevel

    let shownCycles = int <| float wsModel.WaveformColumnWidth / (newZoom * 30.0)

    dispatch <| InitiateWaveSimulation
        {wsModel with
            ZoomLevel = newZoom
            ZoomLevelIndex = newIndex
            ShownCycles = shownCycles
        }

/// Click on these buttons to change the number of visible clock cycles.
let zoomButtons (wsModel: WaveSimModel) (dispatch: Msg -> unit) : ReactElement =
    div [ clkCycleButtonStyle ]
        [
            button [ Button.Props [clkCycleLeftStyle] ]
                (fun _ -> changeZoom wsModel false dispatch)
                zoomOutSVG
            button [ Button.Props [clkCycleRightStyle] ]
                (fun _ -> changeZoom wsModel true dispatch)
                zoomInSVG
        ]

/// Click on these to change the highlighted clock cycle.
let clkCycleButtons (wsModel: WaveSimModel) (dispatch: Msg -> unit) : ReactElement =
    /// Controls the number of cycles moved by the "◀◀" and "▶▶" buttons
    let bigStepSize = max 1 (wsModel.ShownCycles / 2)

    let scrollWaveformsBy (numCycles: int) =
        setClkCycle wsModel dispatch (wsModel.CurrClkCycle + numCycles)

    div [ clkCycleButtonStyle ]
        [
            // Move left by bigStepSize cycles
            button [ Button.Props [clkCycleLeftStyle] ]
                (fun _ -> scrollWaveformsBy -bigStepSize)
                (str "◀◀")

            // Move left by one cycle
            button [ Button.Props [clkCycleInnerStyle] ]
                (fun _ -> scrollWaveformsBy -1)
                (str "◀")

            // Text input box for manual selection of clock cycle
            Input.number [
                Input.Props clkCycleInputProps

                Input.Value (
                    match wsModel.ClkCycleBoxIsEmpty with
                    | true -> ""
                    | false -> string wsModel.CurrClkCycle
                )
                // TODO: Test more properly with invalid inputs (including negative numbers)
                Input.OnChange(fun c ->
                    match System.Int32.TryParse c.Value with
                    | true, n ->
                        setClkCycle wsModel dispatch n
                    | false, _ when c.Value = "" ->
                        dispatch <| SetWSModel {wsModel with ClkCycleBoxIsEmpty = true}
                    | _ ->
                        dispatch <| SetWSModel {wsModel with ClkCycleBoxIsEmpty = false}
                )
            ]

            // Move right by one cycle
            button [ Button.Props [clkCycleInnerStyle] ]
                (fun _ -> scrollWaveformsBy 1)
                (str "▶")

            // Move right by bigStepSize cycles
            button [ Button.Props [clkCycleRightStyle] ]
                (fun _ -> scrollWaveformsBy bigStepSize)
                (str "▶▶")
        ]

/// ReactElement of the tabs for changing displayed radix
let private radixButtons (wsModel: WaveSimModel) (dispatch: Msg -> unit) : ReactElement =
    let radixString = [
        Bin,  "Bin"
        Hex,  "Hex"
        Dec,  "uDec"
        SDec, "sDec"
    ]

    let radixTab (radix, radixStr) =
        Tabs.tab [
            Tabs.Tab.IsActive(wsModel.Radix = radix)
            Tabs.Tab.Props radixTabProps
        ] [ a [
            radixTabAStyle
            OnClick(fun _ -> dispatch <| SetWSModel {wsModel with Radix = radix})
            ] [ str radixStr ]
        ]

    Tabs.tabs [
        Tabs.IsToggle
        Tabs.Props [ radixTabsStyle ]
    ] (List.map (radixTab) radixString)

/// Display closeWaveSimButton, zoomButtons, radixButtons, clkCycleButtons
let waveSimButtonsBar (wsModel: WaveSimModel) (dispatch: Msg -> unit) : ReactElement = 
    div [ waveSimButtonsBarStyle ]
        [
            closeWaveSimButton wsModel dispatch
            zoomButtons wsModel dispatch
            radixButtons wsModel dispatch
            clkCycleButtons wsModel dispatch
        ]

// /// change the order of the waveforms in the simulator
// let private moveWave (model:Model) (wSMod: WaveSimModel) up =
//     let moveBy = if up then -1.5 else 1.5
//     let addLastPort arr p =
//         Array.mapi (fun i el -> if i <> Array.length arr - 1 then el
//                                 else fst el, Array.append (snd el) [| p |]) arr
//     let svgCache = wSMod.DispWaveSVGCache
//     let movedNames =
//         wSMod.SimParams.DispNames
//         |> Array.map (fun name -> isWaveSelected model wSMod.AllWaves[name], name)
//         |> Array.fold (fun (arr, prevSel) (sel,p) -> 
//             match sel, prevSel with 
//             | true, true -> addLastPort arr p, sel
//             | s, _ -> Array.append arr [| s, [|p|] |], s ) ([||], false)
//         |> fst
//         |> Array.mapi (fun i (sel, ports) -> if sel
//                                                then float i + moveBy, ports
//                                                else float i, ports)
//         |> Array.sortBy fst
//         |> Array.collect snd 
//     setDispNames movedNames wSMod
//     |> SetWSModel

// let moveWave (wsModel: WaveSimModel) (direction: bool) (dispatch: Msg -> unit) : unit =
//     ()

/// Create label of waveform name for each selected wave
let nameRows (wsModel: WaveSimModel) : ReactElement list =
    wsModel.SelectedWaves
    |> List.map (fun driver ->
        label [ labelStyle ] [ str wsModel.AllWaves[driver].DisplayName ]
    )

/// Create column of waveform names
let namesColumn wsModel : ReactElement =
    let rows = nameRows wsModel

    div [ namesColumnStyle ]
        (List.concat [ topRow; rows ])

/// Create label of waveform value for each selected wave at a given clk cycle.
let valueRows (wsModel: WaveSimModel) = 
    selectedWaves wsModel
    |> List.map (getWaveValue wsModel.CurrClkCycle)
    |> List.map (valToString wsModel.Radix)
    |> List.map (fun value -> label [ labelStyle ] [ str value ])

/// Create column of waveform values
let private valuesColumn wsModel : ReactElement =
    let rows = valueRows wsModel

    div [ valuesColumnStyle ]
        (List.concat [ topRow; rows ])

/// Generate list of `line` objects which are the background clock lines.
/// These need to be wrapped by an SVG canvas.
let backgroundSVG (wsModel: WaveSimModel) : ReactElement list =
    let clkLine x = 
        line [
            clkLineStyle
            X1 x
            Y1 0.0
            X2 x
            Y2 Constants.viewBoxHeight
        ] []
    [ wsModel.StartCycle + 1 .. endCycle wsModel + 1 ] 
    |> List.map (fun x -> clkLine (float x * wsModel.ZoomLevel))

/// Generate a row of numbers in the waveforms column.
/// Numbers correspond to clock cycles.
let clkCycleNumberRow (wsModel: WaveSimModel) =
    let makeClkCycleLabel i =
        match wsModel.ZoomLevel with
        | width when width < 0.67 && i % 5 <> 0 -> []
        | _ -> [ text (clkCycleText wsModel i) [str (string i)] ]

    [ wsModel.StartCycle .. endCycle wsModel]
    |> List.collect makeClkCycleLabel
    |> List.append (backgroundSVG wsModel)
    |> svg (clkCycleNumberRowProps wsModel)

/// Generate a column of waveforms corresponding to selected waves.
let waveformColumn (wsModel: WaveSimModel) : ReactElement =
    let waveRows : ReactElement list =
        selectedWaves wsModel
        |> List.map (fun wave ->
            match wave.Polylines with
                | Some polylines ->
                    polylines
                // Maybe this shouldn't fail. Could just return a text element saying no waveform was generated
                | None ->
                    printf "no waveform generated for %A" wave.DisplayName
                    [ div [] [] ]//failwithf "No waveform for selected wave %A" wave.DisplayName
            |> List.append (backgroundSVG wsModel)
            |> svg (waveRowProps wsModel)
        )

    div [ waveformColumnStyle ]
        [
            clkCycleHighlightSVG wsModel (List.length wsModel.SelectedWaves)
            div [ waveRowsStyle wsModel.WaveformColumnWidth]
                ([ clkCycleNumberRow wsModel ] @
                waveRows
                )
        ]

/// Display the names, waveforms, and values of selected waveforms
let showWaveforms (wsModel: WaveSimModel) (dispatch: Msg -> unit) : ReactElement =
    div [ showWaveformsStyle ]
        [
            namesColumn wsModel
            waveformColumn wsModel
            valuesColumn wsModel
        ]

let wsClosedPane (model: Model) (dispatch: Msg -> unit) : ReactElement =
    let startButtonOptions = [
        Button.Color IsSuccess
    ]

    let startButtonAction simData reducedState = fun _ ->
        let wsSheet = Option.get (getCurrFile model)
        let wsModel = getWSModel model
        let allWaves =
            getWavesNew simData reducedState
            |> Map.map (generateWaveform wsModel)

        let selectedWaves = List.filter (fun key -> Map.containsKey key allWaves) wsModel.SelectedWaves
        let wsModel = {
            wsModel with
                State = WSOpen
                AllWaves = allWaves
                SelectedWaves = selectedWaves
                OutOfDate = false
                ReducedState = reducedState
        }

        dispatch <| SetWSModelAndSheet (wsModel, wsSheet)

    div [ waveSelectionPaneStyle ]
        [
            Heading.h4 [] [ str "Waveform Simulator" ] 
            str "Some instructions here"

            hr []

            match SimulationView.makeSimData model with
                | None ->
                    div [ errorMessageStyle ]
                        [ str "Please open a project to use the waveform simulator." ]
                | Some (Error e, _) ->
                    displayErrorMessage e
                | Some (Ok simData, reducedState) ->
                    if simData.IsSynchronous then
                        button startButtonOptions (startButtonAction simData reducedState) (str "Start Waveform Simulator")
                    else
                        div [ errorMessageStyle ]
                            [ str "There is no sequential logic in this circuit." ]
        ]

let toggleSelectSubGroup (wsModel: WaveSimModel) dispatch (selected: bool) (waves: Map<WaveIndexT, Wave>) =
    let toggledWaves = Map.keys waves |> Seq.toList
    let selectedWaves =
        if selected then
            List.append wsModel.SelectedWaves toggledWaves
        else
            List.except wsModel.SelectedWaves toggledWaves
    dispatch <| InitiateWaveSimulation {wsModel with SelectedWaves = selectedWaves}

let checkboxRow (wsModel: WaveSimModel) dispatch (index: WaveIndexT) =
    let fontStyle = if isWaveSelected wsModel index then boldFontStyle else normalFontStyle
    tr  [ fontStyle ]
        [
            td  [ noBorderStyle ]
                [ Checkbox.checkbox []
                    [ Checkbox.input [
                        Props (checkboxInputProps @ [
                            OnChange(fun _ -> toggleConnsSelect index wsModel dispatch )
                            Checked <| isWaveSelected wsModel index
                        ])
                    ] ]
                ]
            td  [ noBorderStyle]
                [str wsModel.AllWaves[index].DisplayName]
        ]

let menuSummary menuType =
    let name =
        match menuType with
        | WireLabels -> "Wire Labels"
        | Components comp -> comp

    summary
        summaryProps
        [ str name ]

let labelRows (menuType: SelectionMenu) (labels: WaveIndexT list) (wsModel: WaveSimModel) dispatch : ReactElement =
    let waves =
        match menuType with
        | WireLabels -> Map.filter (fun _ (wave: Wave) -> wave.Type = IOLabel) wsModel.AllWaves
        | Components compLabel -> Map.filter (fun _ (wave: Wave) -> wave.CompLabel = compLabel) wsModel.AllWaves
    let subGroupSelected = Map.forall (fun index _ -> isWaveSelected wsModel index) waves
    
    tr summaryProps [
        th [] [
            Checkbox.checkbox [] [
                Checkbox.input [
                    Props [
                        Checked subGroupSelected
                        OnChange (fun _ -> toggleSelectSubGroup wsModel dispatch (not subGroupSelected) waves)
                    ]
                ]
            ]
        ]
        th [] [
            details
                detailsProps
                [   menuSummary menuType
                    Table.table [] [
                        tbody []
                            (List.map (checkboxRow wsModel dispatch) labels)
                    ]
                ]
        ]
    ]

let componentRows wsModel dispatch ((compName, waves): string * Wave list) : ReactElement =
    let waveLabels =
        List.sortBy (fun (wave: Wave) -> wave.DisplayName) waves
        |> List.map (fun (wave: Wave) -> wave.WaveId)
    labelRows (Components compName) waveLabels wsModel dispatch

let selectWavesMenu (wsModel: WaveSimModel) (dispatch: Msg -> unit) : ReactElement =
    let wireLabelWaves, compWaves = Map.partition (fun _ (wave: Wave) -> wave.Type = IOLabel) wsModel.AllWaves
    let wireLabels =
        wireLabelWaves
        |> Map.values |> Seq.toList
        |> List.sortBy (fun wave -> wave.DisplayName)
        |> List.map (fun wave -> wave.WaveId)

    let compWaveLabels =
        compWaves
        |> Map.values |> Seq.toList
        |> List.sortBy (fun (wave: Wave) -> wave.CompLabel)
        |> List.groupBy (fun wave -> wave.CompLabel)

    div [] [
        Table.table [
            Table.IsBordered
            Table.IsFullWidth
            Table.Props [
                Style [BorderWidth 0]
            ]
        ] [ thead []
                ( [selectAll wsModel dispatch] @
                    [ labelRows WireLabels wireLabels wsModel dispatch ] @
                    (List.map (componentRows wsModel dispatch) compWaveLabels)
                )
        ]
    ]


let wsOpenPane (wsModel: WaveSimModel) dispatch : ReactElement =
    div [ waveSelectionPaneStyle ]
        [
            Heading.h4 [] [ str "Waveform Simulator" ]
            str "Some instructions here"

            hr []

            waveSimButtonsBar wsModel dispatch
            showWaveforms wsModel dispatch

            hr []

            selectWavesMenu wsModel dispatch

            hr []
        ]

/// Entry point to the waveform simulator. This function returns a ReactElement showing
/// either the Wave Selection Pane or the Wave Viewer Pane. The Wave Selection Pane
/// allows the user to select which waveforms they would like to view, and the Wave
/// Viewer Pane displays these selected waveforms, along with their names and values.
let viewWaveSim (model: Model) dispatch : ReactElement =
    let wsModel = getWSModel model
    match wsModel.State with
    | WSClosed ->
        wsClosedPane model dispatch
    | WSOpen ->
        wsOpenPane wsModel dispatch
