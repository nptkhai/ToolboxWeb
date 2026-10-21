// Display formats per UI culture; the same rules as Extensions/DisplayFormatExtensions.cs
// (docs/DESIGN_DECISIONS.md 2.I). vi-VN: dd/MM/yyyy, HH:mm, 17,5. en-US: MM/dd/yyyy, h:mm AM, 17.5.
// Times of day use the app time zone (App:TimeZone on <html data-time-zone>); calendar dates
// (due dates, picker values) are formatted as they are, without a time zone shift.
(function () {
    'use strict';

    const root = document.documentElement;
    const locale = root.lang || 'vi-VN';
    const vietnamese = locale.toLowerCase().startsWith('vi');
    const timeZone = root.dataset.timeZone || undefined;

    function toDate(value) {
        return value instanceof Date ? value : new Date(value);
    }

    function safeFormat(options) {
        try {
            return new Intl.DateTimeFormat(locale, options);
        } catch {
            // Unknown time zone in this browser: fall back to the browser's own.
            const { timeZone: ignored, ...rest } = options;
            return new Intl.DateTimeFormat(locale, rest);
        }
    }

    const dateOptions = { day: '2-digit', month: '2-digit', year: 'numeric' };
    const timeOptions = vietnamese
        ? { hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }
        : { hour: 'numeric', minute: '2-digit', hour12: true };
    const calendarDate = safeFormat(dateOptions);
    const zonedDate = safeFormat({ ...dateOptions, timeZone });
    const zonedTime = safeFormat({ ...timeOptions, timeZone });
    const zonedWeekdayDate = safeFormat({ weekday: 'short', ...dateOptions, timeZone });

    function number(value, maxDecimals) {
        return new Intl.NumberFormat(locale, { maximumFractionDigits: maxDecimals ?? 2 }).format(Number(value) || 0);
    }

    window.Toolbox = window.Toolbox || {};
    window.Toolbox.format = {
        locale: locale,
        timeZone: timeZone,
        /** Kendo DatePicker pattern for the UI culture. */
        datePattern: vietnamese ? 'dd/MM/yyyy' : 'MM/dd/yyyy',
        /** A calendar date (no time zone shift), e.g. a due date. */
        date: function (value) { return calendarDate.format(toDate(value)); },
        /** Date of an instant in the app time zone. */
        instantDate: function (value) { return zonedDate.format(toDate(value)); },
        /** Time of day of an instant in the app time zone. */
        time: function (value) { return zonedTime.format(toDate(value)); },
        dateTime: function (value) { return zonedDate.format(toDate(value)) + ' ' + zonedTime.format(toDate(value)); },
        weekdayDate: function (value) { return zonedWeekdayDate.format(toDate(value)); },
        number: number,
        /** Hours with up to two decimals and an "h" suffix: 17,5h. */
        hours: function (value) { return number(value, 2) + 'h'; }
    };
})();
