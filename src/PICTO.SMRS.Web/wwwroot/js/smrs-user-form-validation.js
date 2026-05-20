(function (global, $) {
    'use strict';

    if (!$ || !$.validator || !$.validator.unobtrusive) {
        return;
    }

    function isSmrsPassword(value) {
        if (!value) {
            return true;
        }
        return value.length >= 8
            && /[A-Z]/.test(value)
            && /[a-z]/.test(value)
            && /[0-9]/.test(value)
            && /[^A-Za-z0-9]/.test(value);
    }

    if (!$.validator.methods.smrspassword) {
        $.validator.addMethod('smrspassword', function (value, element) {
            return this.optional(element) || isSmrsPassword(value);
        });
    }

    if (!$.validator.unobtrusive.adapters._smrsPasswordAdapter) {
        $.validator.unobtrusive.adapters.add('smrspassword', function (options) {
            options.rules.smrspassword = true;
            options.messages.smrspassword = options.message;
        });
        $.validator.unobtrusive.adapters._smrsPasswordAdapter = true;
    }

    function enableEarlyValidation($form) {
        var validator = $form.data('validator');
        if (!validator) {
            return;
        }

        validator.settings.onfocusout = function (element) {
            $(element).valid();
        };

        validator.settings.onkeyup = function (element) {
            var name = element.name || '';
            if (name === 'Password' || name === 'ConfirmPassword' || name === 'NewPassword' || name === 'ConfirmNewPassword') {
                $(element).valid();
                if (name === 'Password' || name === 'NewPassword') {
                    var confirmName = name === 'Password' ? 'ConfirmPassword' : 'ConfirmNewPassword';
                    var $confirm = $form.find('[name="' + confirmName + '"]');
                    if ($confirm.length && $confirm.val()) {
                        $confirm.valid();
                    }
                }
            }
        };
    }

    function focusFirstError($form) {
        var $first = $form.find('.input-validation-error, .field-validation-error:visible').first();
        if (!$first.length) {
            $first = $form.find(':input.error').first();
        }
        if ($first.length) {
            $first.trigger('focus');
        }
    }

    global.smrsInitUserForm = function (formEl, options) {
        var $form = $(formEl);
        if (!$form.length) {
            return;
        }

        options = options || {};

        $.validator.unobtrusive.parse(formEl);
        enableEarlyValidation($form);

        $form.off('submit.smrsUserForm').on('submit.smrsUserForm', function (e) {
            if (!$form.valid()) {
                e.preventDefault();
                e.stopImmediatePropagation();
                focusFirstError($form);
                return false;
            }

            if (options.confirmMessage && !window.confirm(options.confirmMessage)) {
                e.preventDefault();
                e.stopImmediatePropagation();
                return false;
            }
        });
    };

    global.smrsInitCreateUserForm = function (formEl) {
        global.smrsInitUserForm(formEl);
    };
})(window, window.jQuery);
