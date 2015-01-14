﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using JuliusSweetland.ETTA.Enums;
using JuliusSweetland.ETTA.Extensions;
using JuliusSweetland.ETTA.Models;
using JuliusSweetland.ETTA.Properties;
using JuliusSweetland.ETTA.Services;
using JuliusSweetland.ETTA.Static;
using JuliusSweetland.ETTA.UI.ViewModels.Keyboards;
using log4net;
using Microsoft.Practices.Prism.Interactivity.InteractionRequest;
using Microsoft.Practices.Prism.Mvvm;
using Alpha = JuliusSweetland.ETTA.UI.ViewModels.Keyboards.Alpha;
using AlternativeAlpha1 = JuliusSweetland.ETTA.UI.ViewModels.Keyboards.AlternativeAlpha1;
using AlternativeAlpha2 = JuliusSweetland.ETTA.UI.ViewModels.Keyboards.AlternativeAlpha2;
using AlternativeAlpha3 = JuliusSweetland.ETTA.UI.ViewModels.Keyboards.AlternativeAlpha3;
using Currencies1 = JuliusSweetland.ETTA.UI.ViewModels.Keyboards.Currencies1;
using Currencies2 = JuliusSweetland.ETTA.UI.ViewModels.Keyboards.Currencies2;
using Menu = JuliusSweetland.ETTA.UI.ViewModels.Keyboards.Menu;
using NumericAndSymbols1 = JuliusSweetland.ETTA.UI.ViewModels.Keyboards.NumericAndSymbols1;
using NumericAndSymbols2 = JuliusSweetland.ETTA.UI.ViewModels.Keyboards.NumericAndSymbols2;
using PhysicalKeys = JuliusSweetland.ETTA.UI.ViewModels.Keyboards.PhysicalKeys;
using SettingCategories = JuliusSweetland.ETTA.UI.ViewModels.Keyboards.ManipulateWindow;
using YesNoQuestion = JuliusSweetland.ETTA.UI.ViewModels.Keyboards.YesNoQuestion;

namespace JuliusSweetland.ETTA.UI.ViewModels
{
    public class MainViewModel : BindableBase
    {
        #region Fields

        private readonly static ILog Log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private readonly IAudioService audioService;
        private readonly ICalibrationService calibrationService;
        private readonly IDictionaryService dictionaryService;
        private readonly IPublishService publishService;
        private readonly IKeyboardService keyboardService;
        private readonly ISuggestionService suggestionService;
        private readonly ICapturingStateManager capturingStateManager;
        private readonly IInputService inputService;
        private readonly IOutputService outputService;
        private readonly IWindowManipulationService mainWindowManipulationService;
        private readonly List<INotifyErrors> notifyErrorServices; 

        private readonly InteractionRequest<Notification> notificationRequest; 
        private readonly InteractionRequest<Notification> errorNotificationRequest;
        private readonly InteractionRequest<NotificationWithCalibrationResult> calibrateRequest;
        
        private SelectionModes selectionMode;
        private Point? currentPositionPoint;
        private KeyValue? currentPositionKey;
        private Tuple<Point, double> pointSelectionProgress;
        private Dictionary<Rect, KeyValue> pointToKeyValueMap;

        #endregion

        #region Ctor

        public MainViewModel(
            IAudioService audioService,
            ICalibrationService calibrationService,
            IDictionaryService dictionaryService,
            IPublishService publishService,
            IKeyboardService keyboardService,
            ISuggestionService suggestionService,
            ICapturingStateManager capturingStateManager,
            IInputService inputService,
            IOutputService outputService,
            IWindowManipulationService mainWindowManipulationService,
            List<INotifyErrors> notifyErrorServices)
        {
            Log.Debug("Ctor called.");

            this.audioService = audioService;
            this.calibrationService = calibrationService;
            this.dictionaryService = dictionaryService;
            this.publishService = publishService;
            this.keyboardService = keyboardService;
            this.suggestionService = suggestionService;
            this.capturingStateManager = capturingStateManager;
            this.inputService = inputService;
            this.outputService = outputService;
            this.mainWindowManipulationService = mainWindowManipulationService;
            this.notifyErrorServices = notifyErrorServices;

            notificationRequest = new InteractionRequest<Notification>();
            errorNotificationRequest = new InteractionRequest<Notification>();
            calibrateRequest = new InteractionRequest<NotificationWithCalibrationResult>();
            
            SelectionMode = SelectionModes.Key;
            Keyboard = new Alpha();

            SelectKeyboardOnKeyboardSetChanges();
            AttachScratchpadEnabledListener();

            HandleFunctionKeySelectionResult(KeyValues.LeftShiftKey); //Set initial shift state to on
        }

        #endregion

        #region Events

        public event EventHandler<KeyValue> KeySelection;

        #endregion

        #region Properties

        public IInputService InputService { get { return inputService; } }
        public ICapturingStateManager CapturingStateManager { get { return capturingStateManager; } }
        public IOutputService OutputService { get { return outputService; } }
        public IKeyboardService KeyboardService { get { return keyboardService; } }
        public ISuggestionService SuggestionService { get { return suggestionService; } }
        public ICalibrationService CalibrationService { get { return calibrationService; } }

        private IKeyboard keyboard;
        public IKeyboard Keyboard
        {
            get { return keyboard; }
            set { SetProperty(ref keyboard, value); }
        }

        public Dictionary<Rect, KeyValue> PointToKeyValueMap
        {
            set
            {
                if (pointToKeyValueMap != value)
                {
                    pointToKeyValueMap = value;

                    inputService.PointToKeyValueMap = value;
                    SelectionResultPoints = null; //The last selection result points cannot be valid if this has changed (window has moved or resized)
                }
            }
        }

        public SelectionModes SelectionMode
        {
            get { return selectionMode; }
            set
            {
                if (SetProperty(ref selectionMode, value))
                {
                    Log.Debug(string.Format("SelectionMode changed to {0}", value));

                    ResetSelectionProgress();

                    if (inputService != null)
                    {
                        inputService.SelectionMode = value;
                    }
                }
            }
        }

        public Point? CurrentPositionPoint
        {
            get { return currentPositionPoint; }
            set { SetProperty(ref currentPositionPoint, value); }
        }

        public KeyValue? CurrentPositionKey
        {
            get { return currentPositionKey; }
            set { SetProperty(ref currentPositionKey, value); }
        }

        public Tuple<Point, double> PointSelectionProgress
        {
            get { return pointSelectionProgress; }
            private set
            {
                if (SetProperty(ref pointSelectionProgress, value))
                {
                    //if (value != null)
                    //{
                    //    Debug.Print("Point:{0}, Progrss:{1}", value.Item1, value.Item2);
                    //}
                    
                    throw new NotImplementedException("Handling of PointSelection progress has not been implemented yet");
                }
            }
        }

        private List<Point> selectionResultPoints;
        public List<Point> SelectionResultPoints
        {
            get { return selectionResultPoints; }
            set { SetProperty(ref selectionResultPoints, value); }
        }

        private int pointsPerSecond;
        public int PointsPerSecond
        {
            get { return pointsPerSecond; }
            set { SetProperty(ref pointsPerSecond, value); }
        }
        
        public string ApplicationAndSystemInfo
        {
            get
            {
                return string.Format("v{0} | P:{1} {2} | OS:{3} {4} ({5})",
                    DiagnosticInfo.AssemblyVersion,
                    DiagnosticInfo.ProcessBitness,
                    DiagnosticInfo.IsProcessElevated ? "Elevated Permissions" : "Non-Elevated Permissions",
                    DiagnosticInfo.OperatingSystemBitness,
                    DiagnosticInfo.OperatingSystemVersion,
                    DiagnosticInfo.OperatingSystemServicePack);
            }
        }

        private bool scratchpadIsDisabled;
        public bool ScratchpadIsDisabled
        {
            get { return scratchpadIsDisabled; }
            set { SetProperty(ref scratchpadIsDisabled, value); }
        }

        public InteractionRequest<Notification> NotificationRequest { get { return notificationRequest; } }
        public InteractionRequest<Notification> ErrorNotificationRequest { get { return errorNotificationRequest; } }
        public InteractionRequest<NotificationWithCalibrationResult> CalibrateRequest { get { return calibrateRequest; } }
        
        #endregion

        #region Methods

        public void AttachServiceEventHandlers()
        {
            Log.Debug("AttachServiceEventHandlers called.");

            if (notifyErrorServices != null)
            {
                notifyErrorServices.ForEach(s => s.Error += HandleServiceError);
            }

            inputService.PointsPerSecond += (o, value) => { PointsPerSecond = value; };

            inputService.CurrentPosition += (o, tuple) =>
            {
                CurrentPositionPoint = tuple.Item1;
                CurrentPositionKey = tuple.Item2;
            };

            inputService.SelectionProgress += (o, progress) =>
            {
                if (progress.Item1 == null
                    && progress.Item2 == 0)
                {
                    ResetSelectionProgress(); //Reset all keys
                }
                else if (progress.Item1 != null)
                {
                    if (SelectionMode == SelectionModes.Key
                        && progress.Item1.Value.KeyValue != null)
                    {
                        keyboardService.KeySelectionProgress[progress.Item1.Value.KeyValue.Value] =
                            new NotifyingProxy<double>(progress.Item2);
                    }
                    else if (SelectionMode == SelectionModes.Point)
                    {
                        PointSelectionProgress = new Tuple<Point, double>(progress.Item1.Value.Point, progress.Item2);
                    }
                }
            };

            inputService.Selection += (o, value) =>
            {
                Log.Debug("Selection event received from InputService.");

                SelectionResultPoints = null; //Clear captured points from previous SelectionResult event

                if (!capturingStateManager.CapturingMultiKeySelection)
                {
                    audioService.PlaySound(Settings.Default.SelectionSoundFile);
                }

                if (SelectionMode == SelectionModes.Key
                    && value.KeyValue != null)
                {
                    if (KeySelection != null)
                    {
                        Log.Debug(string.Format("Firing KeySelection event with KeyValue '{0}'", value.KeyValue.Value));
                        KeySelection(this, value.KeyValue.Value);
                    }
                }
                else if (SelectionMode == SelectionModes.Point)
                {
                    //TODO: Handle point selection
                }
            };

            inputService.SelectionResult += (o, tuple) =>
            {
                Log.Debug("SelectionResult event received from InputService.");

                var points = tuple.Item1;
                var singleKeyValue = tuple.Item2 != null || tuple.Item3 != null
                    ? new KeyValue { FunctionKey = tuple.Item2, String = tuple.Item3 }
                    : (KeyValue?)null;
                var multiKeySelection = tuple.Item4;

                SelectionResultPoints = points; //Store captured points from SelectionResult event (displayed for debugging)

                if (SelectionMode == SelectionModes.Key
                    && (singleKeyValue != null || (multiKeySelection != null && multiKeySelection.Any())))
                {
                    KeySelectionResult(singleKeyValue, multiKeySelection);
                }
                else if (SelectionMode == SelectionModes.Point)
                {
                    //TODO: Handle point selection result
                }
            };

            inputService.PointToKeyValueMap = pointToKeyValueMap;
            inputService.SelectionMode = SelectionMode;
        }
        
        private void KeySelectionResult(KeyValue? singleKeyValue, List<string> multiKeySelection)
        {
            //Single key string
            if (singleKeyValue != null
                && !string.IsNullOrEmpty(singleKeyValue.Value.String))
            {
                Log.Debug(string.Format("KeySelectionResult received with string value '{0}'", singleKeyValue.Value.String.ConvertEscapedCharsToLiterals()));
                outputService.ProcessSingleKeyText(singleKeyValue.Value.String);
            }

            //Single key function key
            if (singleKeyValue != null
                && singleKeyValue.Value.FunctionKey != null)
            {
                Log.Debug(string.Format("KeySelectionResult received with function key value '{0}'", singleKeyValue.Value.FunctionKey));
                HandleFunctionKeySelectionResult(singleKeyValue.Value);
            }

            //Multi key selection
            if (multiKeySelection != null
                && multiKeySelection.Any())
            {
                Log.Debug(string.Format("KeySelectionResult received with '{0}' multiKeySelection results", multiKeySelection.Count));
                outputService.ProcessMultiKeyTextAndSuggestions(multiKeySelection);
            }
        }

        private void HandleFunctionKeySelectionResult(KeyValue singleKeyValue)
        {
            if (singleKeyValue.FunctionKey != null)
            {
                keyboardService.ProgressKeyDownState(singleKeyValue);

                switch (singleKeyValue.FunctionKey.Value)
                {
                    case FunctionKeys.AddToDictionary:
                        AddTextToDictionary();
                        break;

                    case FunctionKeys.AlphaKeyboard:
                        Log.Debug("Changing keyboard to Alpha.");
                        Keyboard = new Alpha();
                        break;

                    case FunctionKeys.AlternativeAlpha1Keyboard:
                        Log.Debug("Changing keyboard to AlternativeAlpha1.");
                        Keyboard = new AlternativeAlpha1();
                        break;

                    case FunctionKeys.AlternativeAlpha2Keyboard:
                        Log.Debug("Changing keyboard to AlternativeAlpha2.");
                        Keyboard = new AlternativeAlpha2();
                        break;

                    case FunctionKeys.AlternativeAlpha3Keyboard:
                        Log.Debug("Changing keyboard to AlternativeAlpha3.");
                        Keyboard = new AlternativeAlpha3();
                        break;

                    case FunctionKeys.BackFromKeyboard:
                        Log.Debug("Navigating back from keyboard.");
                        var navigableKeyboard = Keyboard as INavigableKeyboard;
                        Keyboard = navigableKeyboard != null && navigableKeyboard.Back != null
                            ? navigableKeyboard.Back
                            : new Alpha();
                        break;

                    case FunctionKeys.Calibrate:
                        if (CalibrationService != null)
                        {
                            Log.Debug("Calibrate requested.");

                            var previousKeyboard = Keyboard;

                            Keyboard = new YesNoQuestion(
                                "Are you sure you would like to re-calibrate?",
                                () =>
                                {
                                    keyboardService.KeyEnabledStates.DisableAll = true;

                                    CalibrateRequest.Raise(new NotificationWithCalibrationResult(), calibrationResult =>
                                    {
                                        if (calibrationResult.Success)
                                        {
                                            NotificationRequest.Raise(new Notification
                                            {
                                                Title = "Success",
                                                Content = calibrationResult.Message
                                            }, __ => { keyboardService.KeyEnabledStates.DisableAll = false; });

                                            audioService.PlaySound(Settings.Default.InfoSoundFile);
                                        }
                                        else
                                        {
                                            if (calibrationResult.Exception != null)
                                            {
                                                keyboardService.KeyEnabledStates.DisableAll = true;

                                                ErrorNotificationRequest.Raise(new Notification
                                                {
                                                    Title = "Uh-oh!",
                                                    Content = calibrationResult.Exception.Message
                                                }, notification => { keyboardService.KeyEnabledStates.DisableAll = false; });

                                                audioService.PlaySound(Settings.Default.ErrorSoundFile);
                                            }
                                        }
                                    });

                                    Keyboard = previousKeyboard;
                                },
                                () =>
                                {
                                    Keyboard = previousKeyboard;
                                });
                        }
                        break;

                    case FunctionKeys.Currencies1Keyboard:
                        Log.Debug("Changing keyboard to Currencies1.");
                        Keyboard = new Currencies1();
                        break;

                    case FunctionKeys.Currencies2Keyboard:
                        Log.Debug("Changing keyboard to Currencies2.");
                        Keyboard = new Currencies2();
                        break;

                    case FunctionKeys.DecreaseOpacity:
                        Log.Debug("Decreasing opacity.");
                        mainWindowManipulationService.DecreaseOpacity();
                        break;

                    case FunctionKeys.ExpandToBottom:
                        Log.Debug(string.Format("Expanding to bottom by {0}px.", Settings.Default.MoveAndResizeAdjustmentAmountInPixels));
                        mainWindowManipulationService.ExpandToBottom(Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        break;

                    case FunctionKeys.ExpandToBottomAndLeft:
                        Log.Debug(string.Format("Expanding to bottom and left by {0}px.", Settings.Default.MoveAndResizeAdjustmentAmountInPixels));
                        mainWindowManipulationService.ExpandToBottomAndLeft(Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        break;

                    case FunctionKeys.ExpandToBottomAndRight:
                        Log.Debug(string.Format("Expanding to bottom and right by {0}px.", Settings.Default.MoveAndResizeAdjustmentAmountInPixels));
                        mainWindowManipulationService.ExpandToBottomAndRight(Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        break;

                    case FunctionKeys.ExpandToLeft:
                        Log.Debug(string.Format("Expanding to left by {0}px.", Settings.Default.MoveAndResizeAdjustmentAmountInPixels));
                        mainWindowManipulationService.ExpandToLeft(Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        break;

                    case FunctionKeys.ExpandToRight:
                        Log.Debug(string.Format("Expanding to right by {0}px.", Settings.Default.MoveAndResizeAdjustmentAmountInPixels));
                        mainWindowManipulationService.ExpandToRight(Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        break;

                    case FunctionKeys.ExpandToTop:
                        Log.Debug(string.Format("Expanding to top by {0}px.", Settings.Default.MoveAndResizeAdjustmentAmountInPixels));
                        mainWindowManipulationService.ExpandToTop(Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        break;

                    case FunctionKeys.ExpandToTopAndLeft:
                        Log.Debug(string.Format("Expanding to top and left by {0}px.", Settings.Default.MoveAndResizeAdjustmentAmountInPixels));
                        mainWindowManipulationService.ExpandToTopAndLeft(Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        break;

                    case FunctionKeys.ExpandToTopAndRight:
                        Log.Debug(string.Format("Expanding to top and right by {0}px.", Settings.Default.MoveAndResizeAdjustmentAmountInPixels));
                        mainWindowManipulationService.ExpandToTopAndRight(Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        break;

                    case FunctionKeys.IncreaseOpacity:
                        Log.Debug("Increasing opacity.");
                        mainWindowManipulationService.IncreaseOpacity();
                        break;

                    case FunctionKeys.ManipulateWindowKeyboard:
                        Log.Debug("Changing keyboard to ManipulateWindow.");
                        Keyboard = new ManipulateWindow(Keyboard);
                        break;

                    case FunctionKeys.MaximiseSize:
                        Log.Debug("Maximising size.");
                        mainWindowManipulationService.Maximise();
                        break;
                        
                    case FunctionKeys.MenuKeyboard:
                        Log.Debug("Changing keyboard to Menu.");
                        Keyboard = new Menu(Keyboard);
                        break;

                    case FunctionKeys.MoveAndResizeAdjustmentAmount:
                        Log.Debug("Progressing MoveAndResizeAdjustmentAmount.");
                        switch (Settings.Default.MoveAndResizeAdjustmentAmountInPixels)
                        {
                            case 1:
                                Settings.Default.MoveAndResizeAdjustmentAmountInPixels = 5;
                                break;

                            case 5:
                                Settings.Default.MoveAndResizeAdjustmentAmountInPixels = 10;
                                break;

                            case 10:
                                Settings.Default.MoveAndResizeAdjustmentAmountInPixels = 25;
                                break;

                            case 25:
                                Settings.Default.MoveAndResizeAdjustmentAmountInPixels = 50;
                                break;

                            case 50:
                                Settings.Default.MoveAndResizeAdjustmentAmountInPixels = 100;
                                break;

                            default:
                                Settings.Default.MoveAndResizeAdjustmentAmountInPixels = 1;
                                break;
                        }
                        break;

                    case FunctionKeys.MoveToBottom:
                        Log.Debug(string.Format("Moving to bottom by {0}px.", Settings.Default.MoveAndResizeAdjustmentAmountInPixels));
                        mainWindowManipulationService.MoveToBottom(Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        break;
                        
                    case FunctionKeys.MoveToBottomAndLeft:
                        Log.Debug(string.Format("Moving to bottom and left by {0}px.", Settings.Default.MoveAndResizeAdjustmentAmountInPixels));
                        mainWindowManipulationService.MoveToBottomAndLeft(Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        break;
                        
                    case FunctionKeys.MoveToBottomAndLeftBoundaries:
                        Log.Debug("Moving to bottom and left boundaries.");
                        mainWindowManipulationService.MoveToBottomAndLeftBoundaries();
                        break;
                        
                    case FunctionKeys.MoveToBottomAndRight:
                        Log.Debug(string.Format("Moving to bottom and right by {0}px.", Settings.Default.MoveAndResizeAdjustmentAmountInPixels));
                        mainWindowManipulationService.MoveToBottomAndRight(Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        break;
                        
                    case FunctionKeys.MoveToBottomAndRightBoundaries:
                        Log.Debug("Moving to bottom and right boundaries.");
                        mainWindowManipulationService.MoveToBottomAndRightBoundaries();
                        break;
                        
                    case FunctionKeys.MoveToBottomBoundary:
                        Log.Debug("Moving to bottom boundary.");
                        mainWindowManipulationService.MoveToBottomBoundary();
                        break;
                        
                    case FunctionKeys.MoveToLeft:
                        Log.Debug(string.Format("Moving to left by {0}px.", Settings.Default.MoveAndResizeAdjustmentAmountInPixels));
                        mainWindowManipulationService.MoveToLeft(Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        break;
                        
                    case FunctionKeys.MoveToLeftBoundary:
                        Log.Debug("Moving to left boundary.");
                        mainWindowManipulationService.MoveToLeftBoundary();
                        break;
                        
                    case FunctionKeys.MoveToRight:
                        Log.Debug(string.Format("Moving to right by {0}px.", Settings.Default.MoveAndResizeAdjustmentAmountInPixels));
                        mainWindowManipulationService.MoveToRight(Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        break;
                        
                    case FunctionKeys.MoveToRightBoundary:
                        Log.Debug("Moving to right boundary.");
                        mainWindowManipulationService.MoveToRightBoundary();
                        break;
                        
                    case FunctionKeys.MoveToTop:
                        Log.Debug(string.Format("Moving to top by {0}px.", Settings.Default.MoveAndResizeAdjustmentAmountInPixels));
                        mainWindowManipulationService.MoveToTop(Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        break;
                        
                    case FunctionKeys.MoveToTopAndLeft:
                        Log.Debug(string.Format("Moving to top and left by {0}px.", Settings.Default.MoveAndResizeAdjustmentAmountInPixels));
                        mainWindowManipulationService.MoveToTopAndLeft(Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        break;
                        
                    case FunctionKeys.MoveToTopAndLeftBoundaries:
                        Log.Debug("Moving to top and left boundaries.");
                        mainWindowManipulationService.MoveToTopAndLeftBoundaries();
                        break;
                        
                    case FunctionKeys.MoveToTopAndRight:
                        Log.Debug(string.Format("Moving to top and right by {0}px.", Settings.Default.MoveAndResizeAdjustmentAmountInPixels));
                        mainWindowManipulationService.MoveToTopAndRight(Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        break;
                        
                    case FunctionKeys.MoveToTopAndRightBoundaries:
                        Log.Debug("Moving to top and right boundaries.");
                        mainWindowManipulationService.MoveToTopAndRightBoundaries();
                        break;
                        
                    case FunctionKeys.MoveToTopBoundary:
                        Log.Debug("Moving to top boundary.");
                        mainWindowManipulationService.MoveToTopBoundary();
                        break;

                    case FunctionKeys.NextSuggestions:
                        Log.Debug("Incrementing suggestions page.");

                        if (suggestionService.Suggestions != null
                            && (suggestionService.Suggestions.Count > (suggestionService.SuggestionsPage + 1) * SuggestionService.SuggestionsPerPage))
                        {
                            suggestionService.SuggestionsPage++;
                        }
                        break;

                    case FunctionKeys.NoQuestionResult:
                        HandleYesNoQuestionResult(false);
                        break;

                    case FunctionKeys.NumericAndSymbols1Keyboard:
                        Log.Debug("Changing keyboard to NumericAndSymbols1.");
                        Keyboard = new NumericAndSymbols1();
                        break;

                    case FunctionKeys.NumericAndSymbols2Keyboard:
                        Log.Debug("Changing keyboard to NumericAndSymbols2.");
                        Keyboard = new NumericAndSymbols2();
                        break;

                    case FunctionKeys.NumericAndSymbols3Keyboard:
                        Log.Debug("Changing keyboard to Symbols3.");
                        Keyboard = new NumericAndSymbols3();
                        break;

                    case FunctionKeys.PhysicalKeysKeyboard:
                        Log.Debug("Changing keyboard to PhysicalKeys.");
                        Keyboard = new PhysicalKeys();
                        break;

                    case FunctionKeys.PreviousSuggestions:
                        Log.Debug("Decrementing suggestions page.");

                        if (suggestionService.SuggestionsPage > 0)
                        {
                            suggestionService.SuggestionsPage--;
                        }
                        break;

                    case FunctionKeys.RestoreSize:
                        Log.Debug("Restoring size.");
                        mainWindowManipulationService.Restore();
                        break;

                    case FunctionKeys.ShrinkFromBottom:
                        Log.Debug(string.Format("Shrinking from bottom by {0}px.", Settings.Default.MoveAndResizeAdjustmentAmountInPixels));
                        mainWindowManipulationService.ShrinkFromBottom(Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        break;

                    case FunctionKeys.ShrinkFromBottomAndLeft:
                        Log.Debug(string.Format("Shrinking from bottom and left by {0}px.", Settings.Default.MoveAndResizeAdjustmentAmountInPixels));
                        mainWindowManipulationService.ShrinkFromBottomAndLeft(Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        break;

                    case FunctionKeys.ShrinkFromBottomAndRight:
                        Log.Debug(string.Format("Shrinking from bottom and right by {0}px.", Settings.Default.MoveAndResizeAdjustmentAmountInPixels));
                        mainWindowManipulationService.ShrinkFromBottomAndRight(Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        break;

                    case FunctionKeys.ShrinkFromLeft:
                        Log.Debug(string.Format("Shrinking from left by {0}px.", Settings.Default.MoveAndResizeAdjustmentAmountInPixels));
                        mainWindowManipulationService.ShrinkFromLeft(Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        break;

                    case FunctionKeys.ShrinkFromRight:
                        Log.Debug(string.Format("Shrinking from right by {0}px.", Settings.Default.MoveAndResizeAdjustmentAmountInPixels));
                        mainWindowManipulationService.ShrinkFromRight(Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        break;

                    case FunctionKeys.ShrinkFromTop:
                        Log.Debug(string.Format("Shrinking from top by {0}px.", Settings.Default.MoveAndResizeAdjustmentAmountInPixels));
                        mainWindowManipulationService.ShrinkFromTop(Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        break;

                    case FunctionKeys.ShrinkFromTopAndLeft:
                        Log.Debug(string.Format("Shrinking from top and left by {0}px.", Settings.Default.MoveAndResizeAdjustmentAmountInPixels));
                        mainWindowManipulationService.ShrinkFromTopAndLeft(Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        break;

                    case FunctionKeys.ShrinkFromTopAndRight:
                        Log.Debug(string.Format("Shrinking from top and right by {0}px.", Settings.Default.MoveAndResizeAdjustmentAmountInPixels));
                        mainWindowManipulationService.ShrinkFromTopAndRight(Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        break;

                    case FunctionKeys.Speak:
                        audioService.Speak(
                            outputService.Text,
                            Settings.Default.SpeechVolume,
                            Settings.Default.SpeechRate,
                            Settings.Default.SpeechVoice);
                        break;

                    case FunctionKeys.YesQuestionResult:
                        HandleYesNoQuestionResult(true);
                        break;
                }

                outputService.ProcessFunctionKey(singleKeyValue.FunctionKey.Value);
            }
        }

        private void AddTextToDictionary()
        {
            Log.Debug("AddTextToDictionary called.");

            var possibleEntries = outputService.Text.ExtractWordsAndLines();

            if (possibleEntries != null)
            {
                var candidates = possibleEntries.Where(pe => !dictionaryService.ExistsInDictionary(pe)).ToList();

                if (candidates.Any())
                {
                    PromptToAddCandidatesToDictionary(candidates, Keyboard);
                }
                else
                {
                    Log.Debug(string.Format("No new words or phrases found in output service's Text: '{0}'.", outputService.Text));

                    keyboardService.KeyEnabledStates.DisableAll = true;

                    NotificationRequest.Raise(new Notification
                    {
                        Title = "Hmm",
                        Content = "It doesn't look like the scratchpad contains any words or phrases that don't already exist in the dictionary."
                    }, notification => { keyboardService.KeyEnabledStates.DisableAll = false; });

                    audioService.PlaySound(Settings.Default.InfoSoundFile);
                }
            }
            else
            {
                Log.Debug(string.Format("No possible words or phrases found in output service's Text: '{0}'.", outputService.Text));

                keyboardService.KeyEnabledStates.DisableAll = true; 

                NotificationRequest.Raise(new Notification
                {
                    Title = "Hmm",
                    Content = "It doesn't look like the scratchpad contains any words or phrases that could be added to the dictionary."
                }, notification => { keyboardService.KeyEnabledStates.DisableAll = false; });

                audioService.PlaySound(Settings.Default.InfoSoundFile);
            }
        }

        private void PromptToAddCandidatesToDictionary(List<string> candidates, IKeyboard originalKeyboard)
        {
            if (candidates.Any())
            {
                var candidate = candidates.First();

                var prompt = candidate.Contains(' ')
                    ? string.Format("Would you like to add the phrase '{0}' to the dictionary with shortcut '{1}'?", 
                        candidate, candidate.CreateDictionaryEntryHash(log: true))
                    : string.Format("Would you like to add the word '{0}' to the dictionary?", candidate);

                if (candidate.Any(Char.IsUpper))
                {
                    prompt = string.Concat(prompt, "\n(The new dictionary entry will contain capital letters)");
                }

                var similarEntries = dictionaryService.GetAllEntriesWithUsageCounts()
                    .Where(de => string.Equals(de.Entry, candidate, StringComparison.InvariantCultureIgnoreCase))
                    .Select(de => de.Entry)
                    .ToList();

                if (similarEntries.Any())
                {
                    string similarEntriesPrompt = string.Format("(FYI some similar entries are already in the dictionary: {0})", 
                        string.Join(", ", similarEntries.Select(se => string.Format("'{0}'", se))));

                    prompt = string.Concat(prompt, "\n\n", similarEntriesPrompt);
                }

                Action nextAction = candidates.Count > 1
                        ? (Action)(() => PromptToAddCandidatesToDictionary(candidates.Skip(1).ToList(), originalKeyboard))
                        : (Action)(() => Keyboard = originalKeyboard);

                Keyboard = new YesNoQuestion(
                    prompt,
                    () =>
                    {
                        dictionaryService.AddNewEntryToDictionary(candidate);

                        keyboardService.KeyEnabledStates.DisableAll = true;

                        NotificationRequest.Raise(new Notification
                        {
                            Title = "Added",
                            Content = string.Format("Great stuff. '{0}' has been added to the dictionary.", candidate)
                        }, notification => { keyboardService.KeyEnabledStates.DisableAll = false; });

                        nextAction();
                    },
                    () => nextAction());
            }
        }

        private void HandleYesNoQuestionResult(bool yesResult)
        {
            Log.Debug(string.Format("YesNoQuestion result of '{0}' received.", yesResult ? "YES" : "NO"));

            var yesNoQuestion = Keyboard as YesNoQuestion;
            if (yesNoQuestion != null)
            {
                if (yesResult)
                {
                    yesNoQuestion.YesAction();
                }
                else
                {
                    yesNoQuestion.NoAction();
                }
            }
        }

        private void AttachScratchpadEnabledListener()
        {
            KeyValues.KeysWhichPreventTextCaptureIfDownOrLocked.ForEach(kv =>
                keyboardService.KeyDownStates[kv].OnPropertyChanges(s => s.Value)
                    .Subscribe(value => CalculateScratchpadIsDisabled()));

            CalculateScratchpadIsDisabled();
        }

        private void CalculateScratchpadIsDisabled()
        {
            ScratchpadIsDisabled = KeyValues.KeysWhichPreventTextCaptureIfDownOrLocked.Any(kv => 
                keyboardService.KeyDownStates[kv].Value.IsDownOrLockedDown());
        }

        private void SelectKeyboardOnKeyboardSetChanges()
        {
            Settings.Default.OnPropertyChanges(s => s.KeyboardSet).Subscribe(visualMode =>
            {
                //Listen to KeyboardSet changes and reset keyboard to Alpha if mode changed to SpeechOnly
                if (visualMode == KeyboardsSets.SpeechOnly)
                {
                    Keyboard = new Alpha();
                }
            });
        }

        private void ResetSelectionProgress()
        {
            PointSelectionProgress = null;

            if (keyboardService != null)
            {
                keyboardService.KeySelectionProgress.Clear();
            }
        }

        public void HandleServiceError(object sender, Exception exception)
        {
            Log.Error("Error event received from service. Raising ErrorNotificationRequest and playing ErrorSoundFile (from settings)", exception);

            keyboardService.KeyEnabledStates.DisableAll = true;

            ErrorNotificationRequest.Raise(new Notification
            {
                Title = "Uh-oh!",
                Content = exception.Message
            }, notification => { keyboardService.KeyEnabledStates.DisableAll = false; });

            audioService.PlaySound(Settings.Default.ErrorSoundFile);
        }

        #endregion
    }
}
