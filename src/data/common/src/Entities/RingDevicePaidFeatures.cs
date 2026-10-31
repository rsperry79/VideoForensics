namespace VideoForensics.Data.Common.Entities
{
    /// <summary>
    /// Ring-only paid feature flags from a doorbot or stickup cam settings object: <c>cv_paid_features</c> and <c>other_paid_features</c>.
    /// 1:1 with the device, replaced on each sync that carries either group. Typed columns named after the Ring key; null means Ring did not send the key.
    /// </summary>
    public class RingDevicePaidFeatures
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }

        /// <summary>cv_paid_features.baby_cry.</summary>
        public bool? CvPaidBabyCry { get; set; }

        /// <summary>cv_paid_features.car_alarm.</summary>
        public bool? CvPaidCarAlarm { get; set; }

        /// <summary>cv_paid_features.co2_smoke_alarm.</summary>
        public bool? CvPaidCo2SmokeAlarm { get; set; }

        /// <summary>cv_paid_features.dog_bark.</summary>
        public bool? CvPaidDogBark { get; set; }

        /// <summary>cv_paid_features.general_sound.</summary>
        public bool? CvPaidGeneralSound { get; set; }

        /// <summary>cv_paid_features.glass_break.</summary>
        public bool? CvPaidGlassBreak { get; set; }

        /// <summary>cv_paid_features.human.</summary>
        public bool? CvPaidHuman { get; set; }

        /// <summary>cv_paid_features.loitering.</summary>
        public bool? CvPaidLoitering { get; set; }

        /// <summary>cv_paid_features.motion.</summary>
        public bool? CvPaidMotion { get; set; }

        /// <summary>cv_paid_features.other_motion.</summary>
        public bool? CvPaidOtherMotion { get; set; }

        /// <summary>cv_paid_features.package_delivery.</summary>
        public bool? CvPaidPackageDelivery { get; set; }

        /// <summary>cv_paid_features.package_pickup.</summary>
        public bool? CvPaidPackagePickup { get; set; }

        /// <summary>cv_paid_features.cv_triggers.</summary>
        public bool? CvPaidCvTriggers { get; set; }

        /// <summary>cv_paid_features.vehicle.</summary>
        public bool? CvPaidVehicle { get; set; }

        /// <summary>cv_paid_features.animal.</summary>
        public bool? CvPaidAnimal { get; set; }

        /// <summary>other_paid_features.alexa_concierge.</summary>
        public bool? OtherPaidAlexaConcierge { get; set; }

        /// <summary>other_paid_features.sheila_cv.</summary>
        public bool? OtherPaidSheilaCv { get; set; }

        /// <summary>other_paid_features.sheila_recording.</summary>
        public bool? OtherPaidSheilaRecording { get; set; }

        /// <summary>other_paid_features.critical_alerts.</summary>
        public bool? OtherPaidCriticalAlerts { get; set; }

        /// <summary>other_paid_features.system_level_pip.</summary>
        public bool? OtherPaidSystemLevelPip { get; set; }

        /// <summary>other_paid_features.extended_live_view.</summary>
        public bool? OtherPaidExtendedLiveView { get; set; }

        /// <summary>other_paid_features.snapshot_capture_plus.</summary>
        public bool? OtherPaidSnapshotCapturePlus { get; set; }

        /// <summary>other_paid_features.live_speak.</summary>
        public bool? OtherPaidLiveSpeak { get; set; }

        /// <summary>other_paid_features.closed_caption.</summary>
        public bool? OtherPaidClosedCaption { get; set; }

        /// <summary>other_paid_features.daily_digest.</summary>
        public bool? OtherPaidDailyDigest { get; set; }

        /// <summary>other_paid_features.ding_call.</summary>
        public bool? OtherPaidDingCall { get; set; }

        /// <summary>other_paid_features.natural_language_search.</summary>
        public bool? OtherPaidNaturalLanguageSearch { get; set; }

        /// <summary>other_paid_features.multicam_live_view.</summary>
        public bool? OtherPaidMulticamLiveView { get; set; }

        /// <summary>other_paid_features.package_protection.</summary>
        public bool? OtherPaidPackageProtection { get; set; }

        /// <summary>other_paid_features.alarm_triggered_recording.</summary>
        public bool? OtherPaidAlarmTriggeredRecording { get; set; }

        /// <summary>other_paid_features.recording_24x7.</summary>
        public bool? OtherPaidRecording24x7 { get; set; }

        /// <summary>UTC time this row was written by a sync (not a Ring value).</summary>
        public DateTime UpdatedAtUtc { get; set; }
    }
}
