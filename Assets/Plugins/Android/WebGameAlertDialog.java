package com.xanaduyonder.webgames;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.res.ColorStateList;
import android.graphics.Color;
import android.graphics.Typeface;
import android.graphics.drawable.ColorDrawable;
import android.graphics.drawable.GradientDrawable;
import android.graphics.drawable.RippleDrawable;
import android.view.Gravity;
import android.view.ViewGroup;
import android.view.Window;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.TextView;

/** Centered recovery prompt shared by the packaged WebView games. */
public final class WebGameAlertDialog {
    private static final int PAPER = 0xfff8f7f1, INK = 0xff173d35, MUTED = 0xff52675d;
    private static final int ACCENT = 0xff276653, SECONDARY = 0xffe7ede5;

    private WebGameAlertDialog() {}

    private static int dp(Activity host, int value) {
        return Math.round(value * host.getResources().getDisplayMetrics().density);
    }

    private static GradientDrawable shape(Activity host, int color, int radius) {
        GradientDrawable surface = new GradientDrawable();
        surface.setColor(color);
        surface.setCornerRadius(dp(host, radius));
        return surface;
    }

    private static Button action(Activity host, String label, boolean primary) {
        Button button = new Button(host);
        button.setText(label);
        button.setTextSize(14);
        button.setAllCaps(false);
        button.setSingleLine(false);
        button.setGravity(Gravity.CENTER);
        button.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
        button.setTextColor(primary ? Color.WHITE : INK);
        button.setMinHeight(dp(host, 48));
        button.setMinimumWidth(0);
        button.setPadding(dp(host, 8), dp(host, 8), dp(host, 8), dp(host, 8));
        button.setBackgroundTintList(null);
        button.setBackground(new RippleDrawable(ColorStateList.valueOf(0x22000000),
                shape(host, primary ? ACCENT : SECONDARY, 8), null));
        return button;
    }

    public static AlertDialog create(Activity host, String title, String message, String primaryLabel,
            Runnable primaryAction, String secondaryLabel, Runnable secondaryAction, Runnable cancelAction) {
        int width = Math.min(dp(host, 360), host.getResources().getDisplayMetrics().widthPixels - dp(host, 32));
        ScrollView scroll = new ScrollView(host);
        scroll.setFillViewport(false);
        scroll.setBackground(shape(host, PAPER, 16));
        scroll.setClipToOutline(true);
        LinearLayout card = new LinearLayout(host);
        card.setOrientation(LinearLayout.VERTICAL);
        int inset = dp(host, 20);
        card.setPadding(inset, inset, inset, inset);
        scroll.addView(card);
        TextView heading = new TextView(host);
        heading.setText(title);
        heading.setTextSize(16);
        heading.setTextColor(INK);
        heading.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
        card.addView(heading);
        TextView text = new TextView(host);
        text.setText(message);
        text.setTextSize(14);
        text.setTextColor(MUTED);
        text.setPadding(0, dp(host, 8), 0, dp(host, 20));
        card.addView(text);
        LinearLayout actions = new LinearLayout(host);
        boolean stack = secondaryLabel != null && (width < dp(host, 300)
                || host.getResources().getConfiguration().fontScale > 1.25f);
        actions.setOrientation(stack ? LinearLayout.VERTICAL : LinearLayout.HORIZONTAL);
        card.addView(actions, -1, -2);
        AlertDialog dialog = new AlertDialog.Builder(host, android.R.style.Theme_Material_Light_Dialog_Alert)
                .setView(scroll).setCancelable(true).setOnCancelListener(ignored -> cancelAction.run()).create();
        if (secondaryLabel != null) {
            Button secondary = action(host, secondaryLabel, false);
            LinearLayout.LayoutParams params = stack ? new LinearLayout.LayoutParams(-1, -2)
                    : new LinearLayout.LayoutParams(0, -2, 1);
            if (stack) params.bottomMargin = dp(host, 8);
            else params.setMarginEnd(dp(host, 8));
            actions.addView(secondary, params);
            secondary.setOnClickListener(v -> { dialog.dismiss(); secondaryAction.run(); });
        }
        Button primary = action(host, primaryLabel, true);
        actions.addView(primary, stack ? new LinearLayout.LayoutParams(-1, -2) : new LinearLayout.LayoutParams(0, -2, 1));
        primary.setOnClickListener(v -> { dialog.dismiss(); primaryAction.run(); });
        dialog.setCanceledOnTouchOutside(true);
        dialog.setOnShowListener(ignored -> {
            Window window = dialog.getWindow();
            window.setBackgroundDrawable(new ColorDrawable(Color.TRANSPARENT));
            window.setDimAmount(.40f);
            window.setLayout(width, ViewGroup.LayoutParams.WRAP_CONTENT);
        });
        return dialog;
    }
}
