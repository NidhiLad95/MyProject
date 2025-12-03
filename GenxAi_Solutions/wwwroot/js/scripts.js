/*!
    * Start Bootstrap - SB Admin v7.0.7 (https://startbootstrap.com/template/sb-admin)
    * Copyright 2013-2023 Start Bootstrap
    * Licensed under MIT (https://github.com/StartBootstrap/startbootstrap-sb-admin/blob/master/LICENSE)
    */
    // 
// Scripts
// 

window.addEventListener('DOMContentLoaded', event => {

    // Toggle the side navigation
    const sidebarToggle = document.body.querySelector('#sidebarToggle');
    if (sidebarToggle) {
        // Uncomment Below to persist sidebar toggle between refreshes
        // if (localStorage.getItem('sb|sidebar-toggle') === 'true') {
        //     document.body.classList.toggle('sb-sidenav-toggled');
        // }
        sidebarToggle.addEventListener('click', event => {
            event.preventDefault();
            document.body.classList.toggle('sb-sidenav-toggled');
            localStorage.setItem('sb|sidebar-toggle', document.body.classList.contains('sb-sidenav-toggled'));
        });
    }

});

// var desktopBtn = $("#desktop");
// var mobileBtn = $("#mobile");
// var body = $('body');

// desktopBtn.on('click', function() {
//   body.addClass('large-screen');
//   togglePrimaryButtonStyle($(this));
// })

// mobileBtn.on('click', function() {
//   body.removeClass('large-screen');
//   togglePrimaryButtonStyle($(this));
// })

// function togglePrimaryButtonStyle(el) {
//   var sibling = el.parent('.btn-group').siblings('.btn-group').find('.btn');
//   el.addClass('btn-primary');
//   sibling.removeClass('btn-primary').addClass('btn-default');
// }

//new code

// var desktopBtn = $("#desktop");
// var mobileBtn = $("#mobile");
// var body = $('body');

// function onZoomChange() {
//   var zoomLevel = Math.round((window.outerWidth - 10) / window.innerWidth * 100);
//   var screenWidth = window.innerWidth;

//   if (zoomLevel >= 125 || screenWidth >= 768) {  // example threshold for desktop view, adjust as needed
//     body.addClass('large-screen');
//     togglePrimaryButtonStyle(desktopBtn);
//   } else {
//     body.removeClass('large-screen');
//     togglePrimaryButtonStyle(mobileBtn);
//   }
// }

// function togglePrimaryButtonStyle(el) {
//   var sibling = el.parent('.btn-group').siblings('.btn-group').find('.btn');
//   el.addClass('btn-primary');
//   sibling.removeClass('btn-primary').addClass('btn-default');
// }

// //Detect zoom by listening to window resize event (triggered on zoom by browsers)
// $(window).on('resize', function() {
//   onZoomChange();
// });

// //Initial check on page load
// $(document).ready(function() {
//   onZoomChange();
// });
// //old code

var desktopBtn = $("#desktop");
var mobileBtn = $("#mobile");
var body = $('body');

function onZoomChange() {
  // Calculate approximate zoom level as a percentage
  //var zoomLevel = Math.round(((window.outerWidth - 10) / window.innerWidth) * 100);
  var zoomLevel = Math.round((window.outerWidth - 10) / window.innerWidth * 100);

  // Check screen width for responsive breakpoint (example 768px)
  var screenWidth = window.innerWidth;

  if (zoomLevel >= 125 || screenWidth >= 768) {  // Apply large-screen if zoom >= 150% OR width >= 768px
    body.addClass('large-screen');
    //togglePrimaryButtonStyle(desktopBtn);
  } else {
    body.removeClass('large-screen');
    //togglePrimaryButtonStyle(mobileBtn);
  }
}

function togglePrimaryButtonStyle(el) {
  var sibling = el.parent('.btn-group').siblings('.btn-group').find('.btn');
  el.addClass('btn-primary');
  sibling.removeClass('btn-primary').addClass('btn-default');
}

//// Listen for resize event triggered by zoom or viewport change
//$(window).on('resize', onZoomChange);

//// GOOD (debounced, and NO reload)
//const onResize = _.debounce(() => {
//    recalcLayoutOnly(); // no network/menu rebind
//}, 200);
//$(window).on('resize', onResize);

// Run on page load
$(document).ready(onZoomChange);
$('#change-theme-btn').click(function () { 
    var text = 'Dark Theme';
    // save $(this) so jQuery doesn't have to execute again
    var $this = $('#change-theme-btn');
    if ($this.text() === text) {
        $(this).text('Light Theme').toggleClass("show-text-dark");
        document.body.classList.toggle('dark-theme');
    } else {
        $this.text(text).toggleClass("show-text-dark");;
        document.body.classList.toggle('dark-theme');
    }
});

//// steper form
//// ------------step-wizard-------------
//$(document).ready(function () {
//    $('.nav-tabs > li a[title]').tooltip();
    
//    //Wizard
//    $('a[data-toggle="tab"]').on('shown.bs.tab', function (e) {

//        var target = $(e.target);
    
//        if (target.parent().hasClass('disabled')) {
//            return false;
//        }
//    });

//    $(".next-step").click(function (e) {

//        var active = $('.wizard .nav-tabs li.active');
//        active.next().removeClass('disabled');
//        nextTab(active);

//    });
//    $(".prev-step").click(function (e) {

//        var active = $('.wizard .nav-tabs li.active');
//        prevTab(active);

//    });
//});

//function nextTab(elem) {
//    $(elem).next().find('a[data-toggle="tab"]').click();
//}
//function prevTab(elem) {
//    $(elem).prev().find('a[data-toggle="tab"]').click();
//}


//$('.nav-tabs').on('click', 'li', function() {
//    $('.nav-tabs li.active').removeClass('active');
//    $(this).addClass('active');
//});



// ends

// $(document).ready(function() {
//   $('html').css('zoom', '80%');
// });





